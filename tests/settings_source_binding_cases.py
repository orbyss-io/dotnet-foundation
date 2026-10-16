"""Installed native inspection/export agreement; current fixture declarations only."""
from __future__ import annotations
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import validate_openapi_exporter as fixture


def qualify(work, environment, project, declaration, metadata, source, command):
    spec = importlib.util.spec_from_file_location('review_current_metadata', fixture.ROOT / 'scripts/refresh_feature_metadata.py')
    refresh = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(refresh)
    authored = work / 'Options.cs'
    original_source = authored.read_bytes()
    original_declaration = declaration.read_bytes()
    base = source.replace('\r\n', '\n') + '// Unicode is significant: café λ 漢字\n'
    variants = {'lf': base.encode(), 'crlf': base.replace('\n', '\r\n').encode(),
                'utf8-bom': b'\xef\xbb\xbf' + base.encode(),
                'lone-cr': base.replace('\n', '\r').encode(),
                'no-final-newline': base.rstrip('\n').encode()}
    observed = {}
    try:
        for name, content in variants.items():
            authored.write_bytes(content)
            before = declaration.read_bytes()
            fixture.run(['dotnet', 'msbuild', str(project), '-t:FoundationInspectSettingsSourceBindings', '-p:Configuration=Release'],
                        work, work / ('hash-' + name + '.log'), env=environment)
            reports = list((work / 'obj').rglob('settings-source-bindings.json'))
            assert len(reports) == 1, reports
            report = json.loads(reports[0].read_text())
            assert declaration.read_bytes() == before, 'Inspection rewrote reviewed declaration'
            expected = refresh.settings_source_hash(authored)
            assert report['sourceSha256'] == {'Options.cs': expected}, report
            observed[name] = expected
            current = copy.deepcopy(metadata)
            current['sourceSha256'] = report['sourceSha256']
            declaration.write_text(json.dumps(current))
            fixture.run(command, work, work / ('hash-export-' + name + '.log'), env=environment)
            emitted = json.loads((work / 'obj/Release/net10.0/orbyss-foundation/settings.json').read_text())
            assert emitted['sourceSha256'] == report['sourceSha256']
        assert observed['lf'] == observed['crlf'] == observed['utf8-bom']
        assert observed['lone-cr'] != observed['lf'] and observed['no-final-newline'] != observed['lf']
        report_bytes = reports[0].read_bytes()
        for name, content in {'invalid-utf8': b'\xff\xfeinvalid', 'utf16': base.encode('utf-16'),
                              'utf32': base.encode('utf-32')}.items():
            authored.write_bytes(content)
            fixture.run(['dotnet', 'msbuild', str(project), '-t:FoundationInspectSettingsSourceBindings', '-p:Configuration=Release'],
                        work, work / ('hash-reject-' + name + '.log'), env=environment, expected=1)
            assert reports[0].read_bytes() == report_bytes, name
            try: refresh.settings_source_hash(authored)
            except UnicodeError: pass
            else: raise AssertionError('Refresh helper accepted ' + name)
        (work / 'source-binding-results.json').write_text(json.dumps(dict(
            contract='utf8-optional-bom-crlf-to-lf-v1', observed=observed,
            nativeInspectionAndCompiledExportAgree=True, inspectionDoesNotRewriteDeclaration=True,
            invalidEncodingPreservesPriorReport=True), indent=2) + '\n')
    finally:
        authored.write_bytes(original_source)
        declaration.write_bytes(original_declaration)

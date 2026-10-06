from pathlib import Path
import argparse
import subprocess
import sys

VALIDATORS = (
    "validate_analyzer.py",
    "validate_authentication_provider_boundary.py",
    "validate_bff_cookie_options.py",
    "validate_assurance.py",
    "validate_client_credentials.py",
    "validate_token_exchange.py",
    "validate_downstream_api.py",
    "validate_dpop.py",
    "validate_jwks_rotation.py",
    "validate_domain_events.py",
    "validate_keycloak_admin.py",
    "validate_web_policies.py",
    "validate_json.py",
    "validate_problem_details.py",
    "validate_json_admission.py",
    "validate_collection_json_contracts.py",
    "validate_postgresql.py",
    "validate_hosted_pages.py",
    "validate_openapi_exporter.py",
    "validate_host_release_payload.py",
    "validate_host_settings_assembly.py",
    "validate_host_settings_owner.py",
    "validate_host_vendor_settings.py",
)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--postgresql-deadline-only", action="store_true",
                        help="Explicit deadline-only provider scope for Windows CI; Linux and Release run native PostgreSQL.")
    arguments = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    for validator in VALIDATORS:
        command = [sys.executable, str(root / "tests" / validator)]
        if validator == "validate_host_release_payload.py":
            command.append("--self-test")
        if arguments.postgresql_deadline_only and validator == "validate_postgresql.py":
            command.append("--deadline-only")
        subprocess.run(command, cwd=root, check=True)
    print("All Foundation focused validators passed." if not arguments.postgresql_deadline_only
          else "Foundation focused validators passed; this run exercises PostgreSQL deadlines only.")

if __name__ == "__main__":
    main()

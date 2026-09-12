from pathlib import Path
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
    "validate_hosted_pages.py",
)

def main():
    root = Path(__file__).resolve().parents[1]
    for validator in VALIDATORS:
        subprocess.run([sys.executable, str(root / "tests" / validator)], cwd=root, check=True)
    print("All Foundation focused validators passed.")

if __name__ == "__main__":
    main()

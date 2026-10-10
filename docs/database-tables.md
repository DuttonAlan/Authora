Entity	Schema	Reason
Users	identity	Core identity record
Password hashes / credentials	identity	Belong to the user's identity and credential lifecycle
Roles and groups	identity	Identity and access-management configuration
OAuth clients	identity	Registered applications
Client secrets	identity	Credentials belonging to a registered client; store securely
Browser/login sessions	auth	Runtime authentication state
Authorization codes	auth	Short-lived OAuth protocol state
Refresh tokens	auth	Long-lived token state with revocation and rotation
Access tokens	auth or no table	Depends on whether you persist or introspect them
OIDC signing keys	Separate key-management component/table	Key lifecycle and rotation deserve their own boundary
Audit events	audit later, or initially auth	Cross-cutting security history
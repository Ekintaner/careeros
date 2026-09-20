# Application boundary

Authentication contains safe AccountView contracts, password validation, ICurrentUser and IPasswordResetDelivery. Infrastructure implements persistence/credential orchestration and API supplies the authenticated current user. Future product use cases must use the server-scoped ownership boundary documented in docs/authentication.md. No fake current user or browser-authoritative UserId exists.

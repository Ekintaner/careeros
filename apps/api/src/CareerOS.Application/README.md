# Application boundary

Authentication contains safe AccountView contracts, password validation, ICurrentUser and IPasswordResetDelivery. Infrastructure implements persistence/credential orchestration and API supplies the authenticated current user. Profiles contains allowlisted product DTOs, profile/skill validation and conflict contracts. ProfileService uses the existing domain entities and server-scoped ownership boundary; see docs/profile.md. Future product use cases must continue using this boundary. No fake current user or browser-authoritative UserId exists.

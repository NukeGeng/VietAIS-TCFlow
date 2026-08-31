# M11 PlatformAdministration event model

Status: `CONFIRMED` for the vNext policy/provider metadata reference slice.

Platform AI policy and provider availability are evented as platform-scoped
settings. Secret material is intentionally excluded; credentials remain in
secret configuration. Every mutation appends a `PlatformAdminActionAudited`
event in the same stream, while the inline policy projection supplies current
runtime decisions. Full FSH Identity permission wiring and audit query APIs
remain cutover work and are not claimed by this slice.

# M12 Vue bounded-context workspace

Status: `CONFIRMED` for the navigation/module-map slice.

Project navigation is defined in `src/modules/navigation.ts` instead of being
duplicated in the shell. Route metadata records the owning backend context
(`planning`, `task-flow`, `architecture`, `access-control`, or
`repository-intelligence`) so future views and clients have a stable mapping.
The selected project remains part of every project route and permission codes
remain UX metadata; backend authorization is still authoritative.

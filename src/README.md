# Icod.Processes source boundary

`Icod.Processes` owns neutral cross-suite process execution and process-control
mechanisms. The implementation was extracted from the former
`Icod.CommandFramework.Processes` namespace without moving suite-specific ProcPs
observation or presentation policy into this package.

The source layer includes:

- executable lookup and immutable child environments;
- exact argument-vector child launching and redirected stream forwarding;
- monotonic execution timeouts through `Icod.Timing`;
- process identity and PID-reuse protection;
- process, process-group, session, and priority-selector target models;
- arbitrary-process liveness and wait operations;
- portable signal parsing, observation, and delivery;
- POSIX launch-time signal policy and process-group creation;
- ordered child-only POSIX file-descriptor duplication at spawn time; and
- POSIX nice values with controlled Windows priority-class substitutions.

ProcPs-specific process enumeration, `/proc` reporting fields, selection grammar,
metrics, personalities, and presentation remain outside this package.

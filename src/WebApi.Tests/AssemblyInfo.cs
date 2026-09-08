// The application under test wires up real Hangfire (AddHangfireServer, Postgres storage) with
// zero test-specific branching, matching production exactly. Hangfire relies on process-wide
// static state (GlobalConfiguration, LibLog's current log provider) that isn't safe to initialize
// from multiple independently-built hosts running at once, so the whole suite runs sequentially:
// only one WebApi host is ever live at a time.
[assembly: NotInParallel]

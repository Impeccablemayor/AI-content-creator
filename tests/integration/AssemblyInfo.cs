using Xunit;

// The factory configures the API through environment variables, which are per
// process, not per test. Running classes in parallel would let one class's
// connection strings overwrite another's while a host was still being built, and
// the failure would look like a schema or a timeout problem. Serialising the
// assembly costs a few seconds and removes that entire class of flake.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
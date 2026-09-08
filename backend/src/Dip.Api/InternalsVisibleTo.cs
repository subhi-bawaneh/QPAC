using System.Runtime.CompilerServices;

// The integration tests drive internal services (the effective document set, the
// import and sync services) directly rather than only through HTTP.
[assembly: InternalsVisibleTo("Dip.Api.IntegrationTests")]

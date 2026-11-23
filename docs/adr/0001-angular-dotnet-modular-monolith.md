# ADR 0001: Angular front end and a modular ASP.NET Core monolith

- **Status:** Accepted
- **Date:** 2026-11-20

## Context

CircleShift needs a member UI, a moderator UI, a transactional API (exchanges, sessions, the time-credit ledger), real-time updates for messages and exchange status, and background work for embeddings and moderation suggestions. One developer builds it in a few weeks, so operational overhead must stay low, but the matching, ledger and moderation modules should be separable later.

## Decision

- **Front end:** a single Angular app in `apps/web`, built with the Angular CLI. Standalone components, lazy routes per feature, Signals for local UI state and RxJS for HTTP and SignalR streams. Strict TypeScript and strict template checking are on from day one.
- **Back end:** one ASP.NET Core deployable in `services/api`, split into `Api`, `Application`, `Domain` and `Infrastructure` projects plus tests. Domain rules (ledger balance, exchange transitions, block checks) live in `Domain` with no framework dependencies.
- **Real-time:** a SignalR hub hosted in the same API process. Hubs only *notify*; the database is the source of truth, and clients reload state over HTTP after reconnecting.
- **Data:** PostgreSQL with pgvector for relational data and embeddings, Redis for caching and the SignalR backplane later. EF Core migrations are committed to the repository.
- **Background work:** hosted services in the API process read an outbox table at first; they can move to a separate worker deployable without changing the domain code.
- **AI:** model calls go through `Microsoft.Extensions.AI` abstractions behind application ports, with a mock implementation for tests and CI. Hard eligibility filters (blocks, visibility, language, availability) are plain .NET query code and are applied before any model ranking.

## Consequences

- One API to run locally and deploy; module boundaries are enforced by project references instead of network calls.
- SignalR scale-out will need a Redis backplane once there is more than one API instance.
- Angular route guards are a convenience only. Every endpoint and hub method checks authorization on the server.
- If matching or moderation needs different scaling, it can move to its own service behind the existing ports.

## Alternatives considered

- **Separate microservices per module:** rejected for now; it adds deployment and consistency work (distributed ledger updates) without a current need.
- **Server-rendered UI:** rejected; the real-time chat and match comparison views fit a client app better, and the stack is fixed to Angular.

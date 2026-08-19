# DigiLine architecture

DigiLine uses Clean Architecture with feature-oriented folders inside each layer.

## Dependency direction

```text
DigiLine.API ───────► DigiLine.Application ───────► DigiLine.Domain
      │                         ▲
      └────► DigiLine.Infrastructure ──────────────┘
```

- `DigiLine.Domain` contains entities, value rules, and domain enums. It has no dependency on the other projects.
- `DigiLine.Application` contains use cases, result/request models, and interfaces required by those use cases. It depends only on Domain.
- `DigiLine.Infrastructure` implements Application interfaces for persistence, authentication, and tenancy. Database migrations remain here.
- `DigiLine.API` is the composition and HTTP delivery layer. Controllers and their HTTP-only request models are grouped by feature.
- Each `*.Tests` project mirrors the area of the production project that it verifies.

## Folder conventions

```text
DigiLine.API/
  Features/<Feature>/       controllers and HTTP contracts
  Middleware/               cross-cutting HTTP behavior
  Realtime/<Feature>/       SignalR hubs and event publishers
  Development/              development-only setup

DigiLine.Application/
  <Feature>/<UseCase>/       service, interface, and result models for one use case
  <Feature>/Common/          ports and models shared within that feature
  Common/                    genuinely shared application concerns
  Transactions/             transaction boundary abstraction

DigiLine.Domain/
  Entities/                  domain entities and their behavior
  Common/                    shared domain types

DigiLine.Infrastructure/
  Authentication/           authentication and password services
  Tenancy/                  current-clinic resolution
  Patients/                  non-persistence patient adapters
  Persistence/
    Configurations/          EF Core entity configurations
    Repositories/<Feature>/ feature-specific repository adapters
    Transactions/           EF Core transaction implementation
  Migrations/               EF Core migration history

digiline.web/src/
  app/                       routing and application layout
  api/                       shared HTTP transport
  auth/                      session state and route protection
  features/<Feature>/        pages, feature API functions, styles, and tests
  realtime/                  shared SignalR hooks
  shared/                    reusable types, UI, and design tokens
  test/                      test runtime setup
```

## Placement rules

1. Put business rules on Domain entities when the rule belongs to the entity.
2. Put workflow orchestration in an Application feature service.
3. Define external dependencies as Application interfaces and implement them in Infrastructure.
4. Keep controllers thin: validate HTTP input, call one Application use case, and translate the result.
5. Do not reference Infrastructure or API from Application or Domain.
6. Add tests under the matching feature or technical area instead of at a test project's root.
7. When a feature grows beyond a few files, group it by use case rather than by generic `Services`, `Interfaces`, or `Models` folders.
8. Keep frontend code local to its feature until at least two features genuinely share it.
9. Keep generated output (`bin`, `obj`, `dist`) and local IDE settings out of architectural folders.

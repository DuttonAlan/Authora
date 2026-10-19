# Run Authora from source

This guide shows how to build and run Authora directly with the .NET SDK, e.g. to develop it or debug it. To only try Authora out, [Getting started](../tutorials/getting-started.md) with Docker is quicker.

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- PostgreSQL 17, e.g. `docker run -d --name authora-db -p 5432:5432 -e POSTGRES_PASSWORD=12345 postgres:17-alpine`
- OpenSSL
- Docker, only to run the tests

## 1. Configure the database

`src/Authora.Api/appsettings.Development.json` expects PostgreSQL on `localhost:5432` with user `postgres` and password `12345`. To use a different database without changing that file, set the connection string as an environment variable in the shell where you run the commands below:

```sh
export ConnectionStrings__Database="Host=localhost;Database=authora;Username=…;Password=…"
```

## 2. Create the database schema

Authora never migrates the database at startup:

```sh
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/Authora.Infrastructure --startup-project src/Authora.Api
```

## 3. Create a signing key

```sh
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out src/Authora.Api/signing-key.pem
```

Git ignores `*.pem` files.

## 4. Run

```sh
dotnet run --project src/Authora.Api --launch-profile https
```

Use the `https` profile: sign-in needs HTTPS. Trust the .NET development certificate once with `dotnet dev-certs https --trust`.

| URL | What |
|---|---|
| <https://localhost:7178/admin/> | Admin interface |
| <https://localhost:7178/swagger> | Swagger UI of the admin API (only in `Development`) |
| <https://localhost:7178/.well-known/openid-configuration> | Discovery document |

`appsettings.Development.json` creates a first administrator, `admin@authora.de` with the password `AuthoraAdmin123!`. These credentials are for local development only; never use that file outside your computer.

## 5. Build and test

```sh
dotnet build Authora.slnx
dotnet test --solution Authora.slnx
```

The tests start PostgreSQL with Testcontainers, so Docker must be running. See [CONTRIBUTING.md](../../CONTRIBUTING.md) for more, e.g. how to run a single test.

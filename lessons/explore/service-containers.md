---
title: "Lab 17: Run integration tests with a service container"
summary: "Start a PostgreSQL database next to a job with a service container, wait for it to be healthy, and run integration tests against it."
duration: 30
difficulty: Intermediate
order: 17
---

# Lab 17: Run integration tests with a service container

The tests from Lab 2 check the calculator on its own. Integration tests check it together with the things it talks to, such as a database, and those things have to be running for the tests to mean anything. In this lab, you'll run the workshop's integration tests twice: once without a database, where they skip, and once with a PostgreSQL database that GitHub Actions starts for the job as a service container.

## Before you start

Finish the foundation labs, [Lab 0](../foundation/00-create-repository.md) to [Lab 2](../foundation/02-test-pull-request.md), first. They cover the workflow basics this lab builds on.

This lab works in the repository you created in Lab 0. Everything happens in your browser, and you don't need Docker or a database on your computer.

If your repository is public, the runs are free. If it's private, they use your account's included Actions minutes. Each run has two jobs of about a minute each, so the lab uses about 6 of the 2,000 minutes a month that GitHub Free includes. See [Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing).

## The integration tests

Your repository already has the tests, in [tests/WorkshopCore.IntegrationTests](../../tests/WorkshopCore.IntegrationTests/CalculationStoreTests.cs). There are three:

- `Postgres_can_be_reached` connects and asks the server for its version.
- `Postgres_and_the_calculator_agree` has PostgreSQL add and subtract two numbers, and checks that the calculator gets the same answers.
- `Saved_results_can_be_read_back` saves two results in a table and reads them back.

Each test reads a connection string from the `WORKSHOP_POSTGRES` environment variable. If it isn't set, the test is skipped instead of failing, so the tests don't break a build that has no database. That's a common pattern: the database is something the workflow provides, not something the code assumes.

## Add the workflow

1. Select the **Code** tab of your repository, then **Add file**, then **Create new file**.
2. In the file name box, type `.github/workflows/integration-tests.yml`.
3. Copy this workflow into the editor.

```yaml source=solutions/service-containers/integration-tests.yml
name: Integration tests

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  without-database:
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false

      - name: Set up .NET
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json

      - name: Run the integration tests
        run: dotnet test tests/WorkshopCore.IntegrationTests

  with-database:
    runs-on: ubuntu-latest
    timeout-minutes: 10
    services:
      postgres:
        image: postgres@sha256:2d2b8998d31037bf721cfdf764d76ba74171b4fab3431b7f72c27c56ddbdf9e3 # 17
        env:
          POSTGRES_PASSWORD: workshop
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 5s
          --health-timeout 5s
          --health-retries 10
    env:
      WORKSHOP_POSTGRES: Host=localhost;Port=5432;Username=postgres;Password=workshop;Database=postgres
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false

      - name: Set up .NET
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json

      - name: Run the integration tests
        run: dotnet test tests/WorkshopCore.IntegrationTests
```

4. Select **Commit changes**, then **Commit changes** again in the dialog to commit to `main`.

The two jobs have the same steps. The difference is in `with-database`:

- `services` lists containers to start before the job's steps, and stop after them. `postgres` is the service's name.
- `image` is the container image, the official PostgreSQL 17 image. It's pinned to a digest, the image's `sha256`, for the same reason actions are pinned to a commit SHA: a tag like `postgres:17` can change, and a digest can't.
- `env` sets environment variables in the container. The PostgreSQL image needs `POSTGRES_PASSWORD` to start, and uses it for the `postgres` user.
- `ports` maps port 5432 in the container to port 5432 on the runner, so the job's steps can connect to `localhost:5432`.
- `options` passes extra settings to Docker when it creates the container. These four set a health check: Docker runs `pg_isready` every 5 seconds, and the runner waits until it succeeds before it starts the steps, so the tests don't run before the database is ready.
- The job's `env` sets `WORKSHOP_POSTGRES` for every step, with the same user and password.

The password is in the workflow because the database only exists for the length of the job, on a runner no one else can reach, and it holds nothing. A password for a database that lasts longer, or that anyone else uses, belongs in a secret.

## Run it

1. Select the **Actions** tab, then **Integration tests** in the list of workflows.
2. Select **Run workflow**, then **Run workflow** in the menu.
3. Open the run when it appears. The two jobs run at the same time.

Open the `without-database` job, and expand **Run the integration tests**. The three tests are skipped, and the step passes:

```text
Skipped! - Failed:     0, Passed:     0, Skipped:     3, Total:     3
```

Now open the `with-database` job. It has two steps you didn't write:

- **Initialize containers** pulls the PostgreSQL image, starts it, and waits for the health check. Expand it to see the `docker pull` and `docker create` commands the runner used, with your `options`, then a line that says the `postgres` service is healthy.
- **Stop containers**, at the end, removes the container and its network.

In between, **Run the integration tests** connects to the database, and all three pass:

```text
Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3
```

### See a failure

1. Open `.github/workflows/integration-tests.yml`, and select the pencil icon.
2. In the `WORKSHOP_POSTGRES` line, change `Password=workshop` to `Password=wrong`.
3. Commit to `main`, and run the workflow again.

The `with-database` job fails. The database started and was healthy, but the tests couldn't log in, and each one reports `28P01: password authentication failed for user "postgres"`. A test that's skipped or passes doesn't tell you the setup is right, so it's worth seeing it fail once.

Change it back to `Password=workshop`, and commit.

## Service containers and job containers

The steps in this lab run on the runner itself, so they reach the service through the port mapped on `localhost`. A job can also run its steps inside a container, called a job container, with `container:`. Then the job and its services are on the same Docker network, and steps reach a service by its name, with no `ports` needed:

```yaml
jobs:
  with-database:
    runs-on: ubuntu-latest
    container: mcr.microsoft.com/dotnet/sdk:10.0
    services:
      postgres:
        image: postgres:17
        env:
          POSTGRES_PASSWORD: workshop
    env:
      WORKSHOP_POSTGRES: Host=postgres;Port=5432;Username=postgres;Password=workshop;Database=postgres
```

`Host=postgres` is the service's name. A job container is useful when the build needs tools the runner doesn't have, because the image brings them. The tradeoff is that every step, including actions, runs in that image, and some actions expect what's on the runner. In a real workflow, pin both images to a digest, as the lab's workflow does.

Service containers and job containers need Docker, so they only work on Linux runners: GitHub's `ubuntu` runners, or self-hosted Linux runners with Docker installed. See [About service containers](https://docs.github.com/en/actions/tutorials/use-containerized-services/use-docker-service-containers) and [Creating PostgreSQL service containers](https://docs.github.com/en/actions/tutorials/use-containerized-services/create-postgresql-service-containers).

## What happened

The runner started a PostgreSQL container before the `with-database` job's steps, mapped its port to the runner, and waited for the health check to pass. The tests found the connection string in `WORKSHOP_POSTGRES`, connected, and ran against a real database, and the runner removed the container when the job ended. The `without-database` job ran the same tests without the variable, and they skipped. Every run gets a new, empty database, so the tests don't depend on what an earlier run left behind.

## Check your result

- The latest **Integration tests** run passed.
- `without-database` shows `Skipped: 3`, and `with-database` shows `Passed: 3`.
- **Initialize containers** in `with-database` shows the `postgres` service is healthy.

## If something fails

If **Initialize containers** fails with an error about the image, check the `image:` line. It must match the workflow above exactly, including the whole digest after `sha256:`.

If **Initialize containers** waits and then fails because the service isn't healthy, check `options`. The `>-` after `options:` joins the four lines into one, and each line needs the same indentation.

If the tests fail with `Failed to connect` or `Connection refused`, check that `ports` is `5432:5432` and the connection string says `Host=localhost;Port=5432`.

If the tests fail with `password authentication failed`, the password in `WORKSHOP_POSTGRES` doesn't match `POSTGRES_PASSWORD`.

If `with-database` shows `Skipped: 3`, the tests didn't see `WORKSHOP_POSTGRES`. Check that `env:` is indented under the job, at the same level as `services:`, and that the variable's name is spelled the same.

Compare your file with [the solution](../../solutions/service-containers/integration-tests.yml).

## Clean up

The workflow only runs when you start it, so it costs nothing to keep. To remove it, delete `.github/workflows/integration-tests.yml`.

## Next step

To see a demo of a workflow that asks an AI model to check code against a team's coding standards, read [the Foundry coding standards lab](foundry-standards.md). Or pick another lab from [the lesson list](../../README.md).

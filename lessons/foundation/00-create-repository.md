---
title: "Lab 0: Create your repository"
summary: "Create your own copy of the workshop repository from its template, so you have somewhere to run workflows."
duration: 5
difficulty: Beginner
order: 0
---

# Lab 0: Create your repository

Every lab runs workflows in a repository that you own. You'll create that repository now from the workshop template. You don't need to install anything.

## Before you start

You need a GitHub account. If you don't have one, [create an account on GitHub](https://github.com/signup). The free plan is enough for the required labs.

## Create your copy

1. Sign in to GitHub. The **Use this template** button only appears when you're signed in.
2. Open the workshop template at [github.com/sabajamalian/ghaction-workshop](https://github.com/sabajamalian/ghaction-workshop).
3. Select **Use this template**, then **Create a new repository**.
4. Under **Owner**, choose your personal account. An organization works too, but its settings for GitHub Actions apply to your copy and can block the labs.
5. Under **Repository name**, type a name for your copy, such as `actions-workshop`. It only has to be different from the names of your other repositories.
6. Choose **Public** or **Private**. [Public or private](#public-or-private) explains the difference. If you're not sure, choose **Public**.
7. Leave **Include all branches** turned off. Your copy only needs `main`.
8. Select **Create repository**.

GitHub creates the repository and opens it. Its main page says it was **Generated from** `sabajamalian/ghaction-workshop`.

> [!IMPORTANT]
> Use this new repository for the rest of the workshop. When a lab says "your repository," it means this copy, not `sabajamalian/ghaction-workshop`.

## Public or private

Both work for the required labs. They differ in who can see your work and in what the workflow runs cost:

| | Public | Private |
| --- | --- | --- |
| Who can see the code and the workflow runs | Anyone | You, and anyone you give access |
| Cost of runs on standard GitHub-hosted runners | Free | Counts against your plan's included minutes, 2,000 a month on GitHub Free |
| Environments, used in optional Lab 8, on GitHub Free | Available | Not available |

The required labs use a few minutes in total. If you choose **Public**, don't commit passwords, tokens, or other secrets to the repository. The optional labs that need a secret show you how to store it as an encrypted repository secret instead. You can change the visibility later in the repository's **Settings**.

## Find the Actions tab

Under your repository's name, select the **Actions** tab. The left sidebar lists the repository's workflows, including **Build application**, which you'll run in the next lab.

Nothing has run yet. The workflows in this template only run when someone starts them.

## Check your result

Your repository contains the file `.github/workflows/build.yml`, and its **Actions** tab lists **Build application**.

## If something fails

If there's no **Use this template** button, you're probably signed out. GitHub hides the button until you sign in. Sign in, then open the template's main page again.

If GitHub won't accept the repository name, you may already have a repository with that name, for example from an earlier attempt at the workshop. Choose a different name.

If your copy belongs to an organization and GitHub Actions doesn't work in it, an organization owner may have turned off GitHub Actions or allowed only some actions. Those settings apply to every repository in the organization. Ask an owner to change them, or create a new copy under your personal account.

## Next step

In [Lab 1: Build the application](01-build-application.md), you'll run the build workflow in your new repository.

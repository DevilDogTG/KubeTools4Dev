# Project Overview

KubeTools4Dev is a cross-platform desktop application built with Avalonia UI and C# (.NET) for developers to interact with and manage Kubernetes resources.

## Project Structure
- **KubeTools4Dev**: The primary Avalonia application, housing the ViewModels, Views, and UI logic.
- **KubeTools4Dev.Core**: The core library containing models, configuration settings, and services for interacting with Kubernetes clusters (e.g., `KubernetesService`, `PortForwardService`).

## Developer Workflow (Modern Release Flow)
The project has migrated from GitFlow to a Trunk-Based development flow using `main` as the primary branch.
- **Branching**: Features and fixes use short-lived branches off `main`.
- **Integration**: Merges are handled via Pull Requests with a Rebase or Squash strategy to maintain linear history.
- **Automation**: `scripts/create-pr.ps1` automates PR creation/updates, utilizing Gemini or Copilot CLI for AI-generated descriptions.
- **Release**: (Pending refactor) Release process is decoupled from daily integration.

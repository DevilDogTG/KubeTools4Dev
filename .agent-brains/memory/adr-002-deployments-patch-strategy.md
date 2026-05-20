# ADR-002: Deployment Patch Strategy

**Status:** Accepted  
**Date:** 2026-05-20  
**Author role:** architect  
**Team:** dev-team  
**Feature:** Deployments Page (`plan/deployments-page.md`)

---

## Context

The Deployments page requires two mutating Kubernetes API operations:

1. **`PatchDeploymentAsync`** — update replica count (`spec.replicas`) and image tag (`spec.template.spec.containers[0].image`) in a single API call.
2. **`RestartDeploymentAsync`** — trigger a rolling restart without changing the workload configuration (equivalent to `kubectl rollout restart`).

The `k8s` C# client (`KubernetesClient` NuGet) supports three patch types via `V1Patch`:

| Type | Content-Type | Array handling |
|---|---|---|
| JSON Merge Patch (RFC 7396) | `application/merge-patch+json` | Replaces arrays wholesale |
| Strategic Merge Patch | `application/strategic-merge-patch+json` | Merges arrays by declared key |
| JSON Patch (RFC 6902) | `application/json-patch+json` | Operates on specific path+index |

The question was: which patch type to use for each operation?

---

## Decision

### `PatchDeploymentAsync` → Strategic Merge Patch

Use `V1Patch` with `V1Patch.PatchType.StrategicMergePatch`.

**Reason:** The patch must update a container's `image` field inside the `spec.template.spec.containers` array. The Kubernetes API declares `containers` with the merge key `name`. Strategic Merge Patch therefore updates only the container whose `name` matches, leaving all other containers (sidecars, init-containers) untouched.

JSON Merge Patch was rejected because it replaces the entire `containers` array — a partial list would silently delete all unlisted containers. This is a data-loss risk that cannot be recovered from without a redeployment.

JSON Patch was rejected because it addresses containers by array index (`/spec/template/spec/containers/0/image`). If a controller ever reorders containers, the index becomes wrong and the patch would corrupt the wrong container silently.

**Patch body shape:**

```json
{
  "spec": {
    "replicas": <int>,
    "template": {
      "spec": {
        "containers": [
          {
            "name": "<first-container-name>",
            "image": "<imageTag>"
          }
        ]
      }
    }
  }
}
```

The `name` field must be populated from a fresh `ReadNamespacedDeploymentAsync` call immediately before patching. It must not be cached, to avoid stale data after in-cluster mutations.

### `RestartDeploymentAsync` → JSON Merge Patch

Use `V1Patch` with `V1Patch.PatchType.MergePatch`.

**Reason:** The patch targets `spec.template.metadata.annotations`, which is a `map<string,string>`. JSON Merge Patch merges maps key-by-key, so adding/updating one annotation key is safe and leaves all other annotations unchanged. Strategic Merge Patch would also work but is unnecessary for a map — using the simpler type is preferable.

**Patch body shape:**

```json
{
  "spec": {
    "template": {
      "metadata": {
        "annotations": {
          "kubectl.kubernetes.io/restartedAt": "2026-05-20T10:00:00.0000000Z"
        }
      }
    }
  }
}
```

Timestamp: `DateTime.UtcNow.ToString("o")` (ISO 8601 round-trip format with UTC offset).

This is functionally identical to what `kubectl rollout restart` does, as documented in the Kubernetes CLI source.

---

## Consequences

### Positive

- **Data-safe**: Sidecar containers are never deleted by a replica/image patch.
- **Idiomatic**: Strategic Merge Patch is what `kubectl patch` and `kubectl set image` use by default.
- **Minimal API calls**: Both operations are single `PatchNamespacedDeploymentAsync` calls.
- **Rollout restart is stateless**: No deployment spec is permanently changed; the annotation is the trigger.

### Negative / Tradeoffs

- **`PatchDeploymentAsync` requires a pre-read**: Must call `ReadNamespacedDeploymentAsync` to get `containers[0].name` before patching. This adds one extra API round-trip per edit. Risk: the container name could change between the read and the patch (TOCTOU), but this window is milliseconds wide and the consequence is a 404/422 API error caught by the caller.
- **Strategic Merge Patch is less portable**: If a future Kubernetes version or CRD does not support strategic merge, a migration to JSON Merge Patch (with explicit full-array patch) would be needed. This is not a concern for core Deployment objects.
- **Annotation accumulates over time**: Each restart adds/overwrites the `restartedAt` annotation; it is never cleaned up. This is the accepted Kubernetes convention and has negligible storage cost.

---

## Alternatives Considered

| Alternative | Reason rejected |
|---|---|
| JSON Merge Patch for `PatchDeploymentAsync` | Replaces entire `containers` array, deleting sidecars |
| JSON Patch for image update | Index-based; fragile if containers are reordered |
| Replace (HTTP PUT) for both operations | Over-fetches and sends full spec; risks race conditions |
| `kubectl rollout restart` subprocess call | Requires `kubectl` binary on host; not portable; violates provider-agnostic principle |

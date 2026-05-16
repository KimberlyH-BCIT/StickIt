# Copilot Instructions

## Project Guidelines
- Write portfolio-facing documentation that is concise, practical, and evidence-based; avoid overclaiming readiness, coverage, or architecture maturity without proof, and do not write the README like a vendor brochure.
- Remove completed items from documentation audit lists rather than keeping them as verified entries to reduce clutter.
  
## Performance Guidelines
- Prefer performance-focused code changes: cache repeated setup or configuration data (e.g., parsed configs, connections, tokens) to avoid redundant work across calls.
- Minimize exception paths: validate inputs early, fail fast, and keep error-handling paths shallow.
- Reduce allocations and response allocations: reuse stable values, avoid temporary objects, and use pooling or static buffers where appropriate to minimize allocation and GC pressure.
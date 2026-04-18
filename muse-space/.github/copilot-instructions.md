# Copilot Instructions

## 项目指南
- For this project, prefer phased development with a confirmed stage plan first; keep the first version simple and understandable for a solo developer; use `ISkillOrchestrator` for decoupling, ordinary Application Services instead of MediatR, prompt templates from the `prompts/` filesystem, prefer JSON outputs from the model, and design APIs/workflows asynchronously.
- Rename `LocalModelClient` to `OpenRouterLlmClient` for clarity.
- Model output uses JSON format; the backend does lenient parsing (don't fail the entire request on parse error).
- API keys should be in local-only config (appsettings.Development.json or user secrets), not committed to source control.
- Every code submission to the Git repository (including git commit and git push) must first obtain explicit user consent and cannot be executed automatically.
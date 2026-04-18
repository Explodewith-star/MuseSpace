# MuseSpace

MuseSpace is an AI-assisted novel writing system based on locally deployed large language models, designed to help writers efficiently create novels, plan plots, and generate content.

## Project Overview

MuseSpace adopts a modular architecture design inspired by Domain-Driven Design (DDD). The system leverages a Skill orchestration mechanism to invoke locally deployed LLMs for creative tasks such as generating scene drafts and performing consistency checks. Core design principles include:

- **Workflow over Training**: Guiding generation through predefined Prompts and Skill workflows, rather than relying on model fine-tuning.
- **Structured Memory**: Utilizing structured Story Context instead of extended context windows.
- **Local Deployment**: Supports integration with open-source LLMs deployed locally, such as GPT-OSS.

## Technology Stack

- **.NET 10.0** - Main framework
- **ASP.NET Core** - Web API
- **Dependency Injection** - Microsoft.Extensions.DependencyInjection
- **Logging** - Microsoft.Extensions.Logging

## Project Structure

```
src/
├── MuseSpace.Domain/           # Domain layer - Entity and enum definitions
│   └── Entities/
│       ├── StoryProject.cs    # Story project
│       ├── Chapter.cs          # Chapter
│       ├── Scene.cs            # Scene
│       ├── Character.cs       # Character
│       ├── WorldRule.cs       # Worldbuilding rules
│       ├── StyleProfile.cs    # Style profile
│       └── GenerationRecord.cs # Generation record
├── MuseSpace.Contracts/       # Contract layer - API request/response definitions
├── MuseSpace.Application/     # Application layer - Business services and abstract interfaces
│   └── Abstractions/
│       ├── Llm/             # LLM client interface
│       ├── Prompt/           # Prompt template interface
│       ├── Skills/           # Skill interface
│       └── Story/            # Story context interface
├── MuseSpace.Infrastructure/  # Infrastructure layer - Concrete implementations
│   ├── Llm/                 # Local model client
│   ├── Logging/              # Generation logging service
│   ├── Prompt/               # File system Prompt provider
│   └── Story/                # Story context builder
└── MuseSpace.Api/            # API layer - Web interfaces
tests/
└── MuseSpace.UnitTests/      # Unit tests
```

## Core Concepts

### Skill System

Skills are the core execution units of the system, each representing a specific creative task:

- **SceneDraftSkill**: Skill for generating scene drafts
- **SkillOrchestrator**: Skill orchestrator responsible for scheduling and executing skills

### Prompt Templates

Prompt templates are stored in the `prompts/` directory and defined in a structured format:

```
Category: {category}
Version: {version}
system
instruction
context
output_format
```

### Story Context

StoryContext is a structured data object used to build generation context, including:

- Project summary
- Recent chapter summaries
- Character cards
- Worldbuilding rules
- Style requirements
- Scene goals
- Conflict design
- Emotional curve

## Quick Start

### Prerequisites

- .NET 10.0 SDK
- Locally deployed LLM service (e.g., GPT-OSS)

### Configuration

Configure the local model endpoint in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "LLM": {
    "BaseUrl": "http://localhost:8080"
  }
}
```

### Run

```bash
cd src/MuseSpace.Api
dotnet run
```

### API Usage Example

Generate a scene draft:

```bash
curl -X POST http://localhost:5000/api/draft/scene \
  -H "Content-Type: application/json" \
  -d '{
    "storyProjectId": "uuid-here",
    "sceneGoal": "The protagonist confronts the antagonist for the first time",
    "conflict": "Both sides engage in intense combat over a mysterious artifact",
    "emotionCurve": "Tense confrontation -> Unexpected twist",
    "involvedCharacterIds": ["uuid1", "uuid2"]
  }'
```

## Current Phase

The project is currently in **Phase 1: Project Framework Setup**. Completed modules include:

- Solution and project structure
- Minimal Domain layer model
- Application layer abstract interfaces
- Skill skeleton
- Prompt skeleton
- Context Builder skeleton
- API skeleton

## Documentation

For more detailed documentation, see:

- [Technical Design](./Plan.md)
- [Development Roadmap](./docs/DevelopmentPlan.md)
- [Getting Started Guide](./docs/GettingStarted.md)
- [Prompt Template Specification](./docs/PromptConvention.md)

## License

This project is intended solely for learning and communication purposes.
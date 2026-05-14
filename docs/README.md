# StickIt documentation index

This folder holds the longer-form reference material behind the portfolio README.

Use the root `README.md` for the fast project tour. Use this index when you want more detail about architecture, API shape, deployment notes, monitoring hooks, and user flows.

## 🎯 Quick Navigation

| Audience | Primary Documents | Secondary Resources |
|----------|------------------|-------------------|
| **Developers** | [Architecture](ARCHITECTURE.md) • [API](API.md) • [Contributing](CONTRIBUTING.md) | [Testing](../ELKH.Tests/README.md) • [Monitoring](MONITORING.md) |
| **DevOps/SysAdmins** | [Deployment](DEPLOYMENT.md) • [Monitoring](MONITORING.md) | [Architecture](ARCHITECTURE.md) • [Contributing](CONTRIBUTING.md) |
| **End Users** | [User Guide](USER_GUIDE.md) | [API](API.md) |
| **Managers/Stakeholders** | [User Guide](USER_GUIDE.md) • [Architecture](ARCHITECTURE.md) | [Monitoring](MONITORING.md) • [Deployment](DEPLOYMENT.md) |

## Documentation catalog

### Technical documentation

#### [Architecture Guide](ARCHITECTURE.md)
**Purpose**: Architectural overview and implementation notes  
**Audience**: Developers, Architects, Technical Leads  
**Contents**:
- Layering and separation-of-concerns decisions
- Controller and service organization
- Data access patterns and EF Core usage
- Role-based access patterns
- Caching, search, and supporting infrastructure notes
- Tradeoffs and future cleanup opportunities

#### [API Documentation](API.md)
**Purpose**: API reference and integration notes  
**Audience**: Developers, API Consumers, Third-party Integrators  
**Contents**:
- API endpoints with examples
- Authentication and authorization notes
- Request and response shapes
- Error handling patterns and status codes
- Usage notes for local exploration

#### [Testing Guide](../ELKH.Tests/README.md)
**Purpose**: Test strategy, structure, and execution notes  
**Audience**: Developers, QA Engineers, CI/CD Administrators  
**Contents**:
- Unit and integration testing strategy
- Test execution and coverage tooling
- Test host and seeded data notes
- Performance-test experiments and limitations

### Operations documentation

#### [Deployment Guide](DEPLOYMENT.md)
**Purpose**: Deployment options and setup notes  
**Audience**: DevOps Engineers, System Administrators, Release Managers  
**Contents**:
- Local development setup
- Docker-based workflows
- Azure-oriented deployment notes
- Production-oriented ideas that may need additional hardening

#### [Monitoring Guide](MONITORING.md)
**Purpose**: Monitoring and observability notes  
**Audience**: Site Reliability Engineers, DevOps Teams, Support Staff  
**Contents**:
- Application Insights hooks and telemetry notes
- Metrics and health-check coverage
- Troubleshooting and tuning references
- Optional monitoring paths rather than mandatory local setup

### User documentation

#### [User Guide](USER_GUIDE.md)
**Purpose**: Role-based workflow reference  
**Audience**: Customers, Staff, Administrators, Support Teams  
**Contents**:
- Customer shopping workflows
- Staff and admin paths
- Feature notes and quick references
- Troubleshooting guidance

### Contributor documentation

#### [Contributing Guidelines](CONTRIBUTING.md)
**Purpose**: Contribution workflow and standards  
**Audience**: Contributing Developers, Open Source Contributors, Team Members  
**Contents**:
- Development environment setup and prerequisites
- Coding standards and style guidelines
- Git workflow and branch management
- Pull request procedures and code review
- Issue reporting and feature request processes
- Community guidelines and code of conduct

## Notes on scope

This documentation set is useful, but it should be read as project reference material, not as a guarantee that every area is exhaustive or production-ready.

Some guides describe:
- the supported local path
- optional infrastructure or deployment ideas
- implementation intent that may still be evolving

## Current verification snapshot

Latest validated evidence on this branch:
- solution-level test run passing: `378/378`
- main test project passing: `306/306`
- guest checkout regression suite passing: `72/72`

This is the current observed test state, not a promise that future changes will preserve it without re-validation.

### Documentation standards
- **Format**: Markdown with consistent structure and styling
- **Diagrams**: Mermaid diagrams for visual representation
- **Code Examples**: Syntax-highlighted code blocks with explanations
- **Cross-References**: Links between related documents where helpful
- **Maintenance**: Updated as the portfolio branch changes

## Maintenance

When the code changes, the related docs should be updated too, especially for:
1. setup steps
2. public routes and API behavior
3. seeded demo credentials or user flows
4. architecture and deployment tradeoffs

## Documentation roadmap

- Add real screenshots and short demos that match the current UI
- Keep trimming enterprise-sounding wording from portfolio-facing docs
- Clarify which deployment and monitoring paths are optional versus supported locally
- Add more concise “start here” pointers for reviewers

## Internal development documentation

### 📝 [Developer Documentation Guidelines](development/DEVELOPER-DOCUMENTATION-GUIDELINES.md)
**Purpose**: Practical guide for creating and maintaining professional code documentation  
**Audience**: Development Team, Code Reviewers, New Team Members  
**Contents**:
- Documentation checklist for new code
- Documentation patterns by code type (Controllers, Services, Configuration)
- Writing effective documentation and security requirements
- Code review guidelines and quality tracking

### 📋 [Documentation Style Guide](development/DOCUMENTATION-STYLE-GUIDE.md)
**Purpose**: Comprehensive technical standards and formatting guidelines  
**Audience**: All Contributors, Documentation Maintainers  
**Contents**:
- XML documentation standards and patterns
- Table of Contents requirements for large files
- Enterprise-grade documentation templates
- Consistency standards and style enforcement

### 📊 [Documentation Overview](development/DOCUMENTATION-README.md)
**Purpose**: High-level overview of the documentation system and standards  
**Audience**: Team Leads, Project Managers, New Contributors  
**Contents**:
- Documentation philosophy and approach
- System overview and organization principles
- Quality metrics and compliance tracking
- Maintenance procedures and responsibilities

## Internal project analysis

### 📋 [Documentation Compliance Audit](internal/audits/PROJECT-WIDE-DOCUMENTATION-AUDIT.md)
**Purpose**: Comprehensive documentation compliance verification and remediation plan  
**Audience**: Technical Leads, Documentation Champions  
**Contents**:
- Current compliance metrics and gap analysis
- Critical documentation issues requiring immediate attention
- Systematic remediation plan with priorities
- Success metrics and tracking methodology

### 🏗️ [Technical Deep Dive Analysis](internal/analysis/PROJECT-DEEP-DIVE-ANALYSIS.md)
**Purpose**: Comprehensive technical architecture and implementation analysis  
**Audience**: Senior Developers, Architects, Technical Decision Makers  
**Contents**:
- Detailed codebase analysis and architectural patterns
- Security implementation and compliance assessment
- Performance characteristics and optimization opportunities
- Technology stack evaluation and recommendations

### 📈 [Project Cleanup Summary](internal/analysis/PROJECT-CLEANUP-SUMMARY-REPORT.md)
**Purpose**: Code quality improvements and cleanup achievements documentation  
**Audience**: Project Managers, Quality Assurance, Stakeholders  
**Contents**:
- Code quality improvements and refactoring achievements
- Technical debt reduction and maintainability gains
- Documentation standardization impact and metrics
- Future improvement recommendations and roadmap

## Contributing to documentation

### Getting help
- **GitHub Issues**: Report documentation issues or request improvements
- **Pull Requests**: Contribute documentation improvements

### Contributing steps
1. **Identify Gap** - Notice missing or outdated information
2. **Create Issue** - Describe the documentation need or problem
3. **Fork Repository** - Create your own copy for editing
4. **Make Changes** - Follow documentation standards and style guide
5. **Submit PR** - Request review and integration of your changes

## Quick reference

### Essential links
- **Main Repository**: [GitHub - StickIt](https://github.com/Velyene/StickIt)
- **Portfolio README**: [../README.md](../README.md)
- **Test project docs**: [../ELKH.Tests/README.md](../ELKH.Tests/README.md)

---

## Status

Last updated for the current portfolio branch. Treat this index as a map to the detailed docs, not as a claim of exhaustive or enterprise-grade coverage in every area.

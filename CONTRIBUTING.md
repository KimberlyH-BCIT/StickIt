# Contributing to StickIt

Thank you for your interest in contributing to StickIt! This document provides guidelines and instructions for contributing to the project.

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Pull Request Process](#pull-request-process)
- [Issue Guidelines](#issue-guidelines)
- [Testing Requirements](#testing-requirements)

---

## Code of Conduct

By participating in this project, you agree to abide by our Code of Conduct:

- Be respectful and inclusive
- Provide constructive feedback
- Focus on the best outcome for the community
- Show empathy towards other contributors

---

## Getting Started

### Prerequisites

- .NET 10 SDK
- Visual Studio 2022/2026 or VS Code
- Git

### Fork and Clone

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/StickIt.git
   cd StickIt
   ```
3. Add the upstream remote:
   ```bash
   git remote add upstream https://github.com/Velyene/StickIt.git
   ```

---

## Development Setup

### 1. Configure User Secrets

```bash
cd ELKH
dotnet user-secrets init
dotnet user-secrets set "PayPal:ClientId" "test-client-id"
dotnet user-secrets set "PayPal:Secret" "test-secret"
dotnet user-secrets set "ReCaptcha:SiteKey" "test-site-key"
dotnet user-secrets set "ReCaptcha:SecretKey" "test-secret-key"
```

### 2. Restore and Build

```bash
dotnet restore
dotnet build
```

### 3. Run Tests

```bash
cd ELKH.Tests
dotnet test
```

### 4. Run the Application

```bash
cd ELKH
dotnet run
```

---

## Coding Standards

### C# Code Style

We follow Microsoft's C# coding conventions with these specific guidelines:

#### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes | PascalCase | `ProductService` |
| Interfaces | IPascalCase | `IProductService` |
| Methods | PascalCase | `GetProductAsync` |
| Properties | PascalCase | `ProductName` |
| Private fields | _camelCase | `_productService` |
| Local variables | camelCase | `productList` |
| Constants | PascalCase | `MaxRetries` |

#### Code Organization

1. **File Structure**: One class per file, matching filename to class name
2. **Usings**: Group by System, Microsoft, Third-party, Local
3. **Regions**: Use sparingly, prefer well-organized small classes

#### Documentation

- Add XML documentation to all public APIs:
  ```csharp
  /// <summary>
  /// Retrieves a product by its unique identifier.
  /// </summary>
  /// <param name="id">The product's primary key.</param>
  /// <returns>The product if found; otherwise, null.</returns>
  public async Task<ProductVM?> GetByIdAsync(int id)
  ```

- Include TABLE OF CONTENTS in large files (200+ lines):
  ```csharp
  /// <remarks>
  /// TABLE OF CONTENTS (~300 lines)
  /// ================================================================================
  /// 1. Constructor & Dependencies ................................. Lines  40-60
  /// 2. Product Retrieval Operations ............................... Lines  62-120
  /// ...
  /// </remarks>
  ```

### Razor/HTML Guidelines

- Use semantic HTML5 elements
- Include ARIA labels for accessibility
- Use asp-* tag helpers over raw HTML where possible

### JavaScript Guidelines

- Use `escapeHtml()` for any dynamic content
- Include JSDoc comments on functions
- Use `async/await` over `.then()` chains

---

## Pull Request Process

### Before Submitting

1. **Update your fork**:
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

2. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Ensure tests pass**:
   ```bash
   dotnet test
   ```

4. **Check for build warnings**:
   ```bash
   dotnet build --warnaserror
   ```

### PR Requirements

- [ ] Code compiles without errors or warnings
- [ ] All existing tests pass
- [ ] New code has appropriate test coverage (80%+ for new code)
- [ ] Documentation updated if needed
- [ ] Commit messages follow conventional format

### Commit Message Format

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Code style (formatting, semicolons, etc.)
- `refactor`: Code change that neither fixes a bug nor adds a feature
- `test`: Adding or correcting tests
- `chore`: Maintenance tasks

**Examples**:
```
feat(cart): add quantity validation on add-to-cart

fix(search): handle empty query strings gracefully

docs(readme): update setup instructions for .NET 10
```

### Review Process

1. Submit PR against `main` branch
2. Automated CI checks must pass
3. At least one maintainer review required
4. Address any feedback
5. Squash and merge when approved

---

## Issue Guidelines

### Reporting Bugs

Please include:

1. **Environment**: OS, .NET version, browser (if applicable)
2. **Steps to Reproduce**: Clear, numbered steps
3. **Expected Behavior**: What should happen
4. **Actual Behavior**: What actually happens
5. **Screenshots/Logs**: If applicable

Use this template:
```markdown
## Bug Description
Brief description of the bug

## Steps to Reproduce
1. Go to '...'
2. Click on '...'
3. See error

## Expected Behavior
What you expected to happen

## Actual Behavior
What actually happened

## Environment
- OS: Windows 11
- .NET: 10.0.5
- Browser: Chrome 120
```

### Requesting Features

1. Check existing issues first
2. Describe the use case
3. Explain expected behavior
4. Consider including mockups/diagrams

---

## Testing Requirements

### Unit Tests

- Place in `ELKH.Tests/Unit/` directory
- Follow naming convention: `{ClassName}Tests.cs`
- Test method naming: `{Method}_When{Condition}_Should{ExpectedResult}`

Example:
```csharp
[Fact]
public async Task GetByIdAsync_WithValidId_ShouldReturnProduct()
{
    // Arrange
    var product = CreateTestProduct();
    _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);

    // Act
    var result = await _service.GetByIdAsync(1);

    // Assert
    result.Should().NotBeNull();
    result!.ProductId.Should().Be(1);
}
```

### Integration Tests

- Place in `ELKH.Tests/Integration/` directory
- Use `WebApplicationFactory<Program>` for API tests
- Clean up test data after each test

### Coverage Requirements

- Minimum 80% line coverage for new code
- Minimum 80% branch coverage for new code
- Migrations and generated code are excluded

Run coverage report:
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

---

## Questions?

- Open a GitHub Discussion for general questions
- Tag maintainers in issues if urgent
- Check existing documentation first

Thank you for contributing! 🎉

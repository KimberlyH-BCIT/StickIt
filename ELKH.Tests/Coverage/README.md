# Test Coverage Configuration for StickIt E-commerce Project

## Coverage Tools Configuration

### 1. Coverlet Configuration
The project is already configured with coverlet packages in `ELKH.Tests.csproj`:
- `coverlet.collector` - For test discovery and collection
- `coverlet.msbuild` - For MSBuild integration

### 2. Coverage Commands

#### Run Tests with Coverage
```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# Generate coverage with detailed output
dotnet test --collect:"XPlat Code Coverage" --logger trx --results-directory ./TestResults/

# Generate coverage with specific format
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=./coverage/
```

#### HTML Coverage Reports
```bash
# Install ReportGenerator tool (one-time setup)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
reportgenerator -reports:"./coverage.cobertura.xml" -targetdir:"./coverage-report" -reporttypes:Html
```

### 3. Coverage Thresholds
Configure minimum coverage thresholds in test project:

```xml
<PropertyGroup>
  <CollectCoverage>true</CollectCoverage>
  <CoverletOutputFormat>opencover</CoverletOutputFormat>
  <CoverletOutput>./coverage/</CoverletOutput>
  <Threshold>80</Threshold>
  <ThresholdType>line,branch</ThresholdType>
  <ThresholdStat>minimum</ThresholdStat>
</PropertyGroup>
```

## Expected Coverage Results

### High Coverage Areas (Expected 90%+)
- **Controllers**: 8 test suites covering all major endpoints
- **Services**: 5 service classes with comprehensive business logic tests
- **Repositories**: 3 repository classes with data access tests

### Medium Coverage Areas (Expected 70-90%)
- **Models**: Basic property validation and business rules
- **ViewModels**: Data transfer and validation logic
- **Integration paths**: API endpoints and authentication flows

### Areas for Future Coverage Expansion
- **Error handling**: Exception scenarios and edge cases
- **Validation**: Complex business rule validation
- **Integration**: External service integrations (PayPal, email)

## Coverage Quality Metrics

### Line Coverage Target: 80%+
### Branch Coverage Target: 75%+
### Method Coverage Target: 85%+

These targets are appropriate for an e-commerce application with your comprehensive test suite.
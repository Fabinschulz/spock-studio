using Mariliz.Application.Discovery;
using Mariliz.Application.Engineering;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Tests;

// Validação real A4: o próprio Spock perdia Python/LangGraph e Rust/Tauri, e o demo perdia Fastify, Zod, Vitest e TypeScript.
public class LanguageDiscoveryTests
{
    private static Task<DiscoveryReport> Discover(TempProject project) =>
        new Journey(project).Service.DiscoverAsync(new Journey(project).ProjectId, CancellationToken.None);

    private static FrameworkFinding Framework(DiscoveryReport report, string id) => Assert.Single(report.Frameworks, item => item.Id == id);

    [Fact]
    public async Task Python_pelo_pyproject_e_pelo_lock_com_versao_e_frameworks()
    {
        using var project = new TempProject();
        project.Write("intelligence/pyproject.toml", "[tool.ruff]\ntarget-version = \"py312\"\n");
        project.Write("intelligence/requirements.lock", "langgraph==1.0.4 \\\n    --hash=sha256:abc\n    # via -r requirements.in\npydantic==2.11.0\npytest==8.4.1\n");

        var report = await Discover(project);

        var python = Assert.Single(report.Stacks, stack => stack.Id == "python");
        Assert.Equal(("intelligence", "3.12", Confidence.Medium), (python.Root, python.Version, python.Confidence));
        Assert.Contains(python.Evidence, evidence => evidence.Detail == "tool.ruff.target-version=py312");
        Assert.Equal("1.0.4", Framework(report, "langgraph").Version);
        Assert.Equal("python", Framework(report, "pydantic").StackId);
        Assert.Contains(report.Tooling, tool => tool.Kind == "test" && tool.Command == "python -m pytest intelligence");
        Assert.Contains(report.Tooling, tool => tool.Kind == "lint" && tool.Command == "ruff check intelligence");
    }

    [Fact]
    public async Task Python_pelo_project_dependencies_e_versao_fixada_vence_a_declarada()
    {
        using var project = new TempProject();
        project.Write("pyproject.toml", "[project]\nname = \"api\"\nrequires-python = \">=3.11\"\ndependencies = [\n  \"fastapi>=0.115\",\n  \"SQLAlchemy[asyncio]~=2.0\",\n]\n");
        project.Write(".python-version", "3.12.4\n");

        var report = await Discover(project);

        var python = Assert.Single(report.Stacks, stack => stack.Id == "python");
        Assert.Equal(("3.12.4", Confidence.High), (python.Version, python.Confidence));
        Assert.Equal("0.115", Framework(report, "fastapi").Version);
        Assert.Equal("2.0", Framework(report, "sqlalchemy").Version);
    }

    [Fact]
    public async Task Rust_pelo_cargo_com_rust_version_e_tauri()
    {
        using var project = new TempProject();
        project.Write("desktop/host/src-tauri/Cargo.toml",
            "[package]\nname = \"host\"\nedition = \"2021\"\nrust-version = \"1.90\"\n\n[dependencies]\nserde = { version = \"1.0\", features = [\"derive\"] }\n" +
            "tauri = { version = \"2.12.5\", features = [] }\ntokio = \"1.54.1\"\n\n[build-dependencies]\ntauri-build = \"2.7.4\"\n");

        var report = await Discover(project);

        var rust = Assert.Single(report.Stacks, stack => stack.Id == "rust");
        Assert.Equal(("desktop/host/src-tauri", "1.90"), (rust.Root, rust.Version));
        Assert.Contains("Edição 2021 da linguagem.", rust.Notes);
        Assert.Equal("2.12.5", Framework(report, "tauri").Version);
        Assert.Equal("1.54.1", Framework(report, "tokio").Version);
        Assert.Contains(report.Tooling, tool => tool.Command == "cargo test --manifest-path desktop/host/src-tauri/Cargo.toml");
    }

    [Fact]
    public async Task Go_pelo_go_mod_com_a_diretiva_e_o_bloco_require()
    {
        using var project = new TempProject();
        project.Write("svc/go.mod", "module example.com/svc\n\ngo 1.22\n\nrequire (\n\tgithub.com/gin-gonic/gin v1.10.0\n\tgorm.io/gorm v1.25.12 // indirect\n)\n");

        var report = await Discover(project);

        var go = Assert.Single(report.Stacks, stack => stack.Id == "go");
        Assert.Equal(("svc", "1.22", Confidence.High), (go.Root, go.Version, go.Confidence));
        Assert.Equal("1.10.0", Framework(report, "gin").Version);
        Assert.Equal("go", Framework(report, "gorm").StackId);
        Assert.Contains(report.Tooling, tool => tool.Command == "go -C svc test ./...");
    }

    [Fact]
    public async Task Java_pelo_maven_e_pelo_gradle()
    {
        using var maven = new TempProject();
        maven.Write("pom.xml", "<project><properties><java.version>21</java.version></properties><dependencies>" +
            "<dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-web</artifactId></dependency>" +
            "</dependencies></project>");
        maven.Write("mvnw", "#!/bin/sh\n");
        using var gradle = new TempProject();
        gradle.Write("app/build.gradle.kts", "plugins { id(\"org.springframework.boot\") version \"3.3.0\" }\n" +
            "java { toolchain { languageVersion.set(JavaLanguageVersion.of(17)) } }\n" +
            "dependencies { testImplementation(\"org.junit.jupiter:junit-jupiter:5.10.2\") }\n");

        var fromMaven = await Discover(maven);
        var fromGradle = await Discover(gradle);

        Assert.Equal("21", Assert.Single(fromMaven.Stacks, stack => stack.Id == "java").Version);
        Framework(fromMaven, "spring-boot");
        Assert.Contains(fromMaven.Tooling, tool => tool.Command == "./mvnw test");
        Assert.Equal("17", Assert.Single(fromGradle.Stacks, stack => stack.Id == "java").Version);
        Assert.Equal("5.10.2", Framework(fromGradle, "junit").Version);
        Framework(fromGradle, "spring-boot");
        Assert.Contains(fromGradle.Tooling, tool => tool.Command == "gradle test -p app");
    }

    [Fact]
    public async Task Frameworks_do_node_saem_do_package_json_sem_virar_stack()
    {
        using var project = new TempProject();
        project.Write("package.json", "{\"dependencies\":{\"fastify\":\"^5.1.0\",\"zod\":\"^3.24.1\",\"left-pad\":\"1.0.0\"},\"devDependencies\":{\"typescript\":\"~5.7.2\",\"vitest\":\"^3.0.0\"}}");

        var report = await Discover(project);

        Assert.Equal("node", Assert.Single(report.Stacks).Id);
        Assert.Equal(["fastify", "typescript", "vitest", "zod"], report.Frameworks.Select(item => item.Id).Order(StringComparer.Ordinal));
        Assert.Equal("5.7.2", Framework(report, "typescript").Version);
        Assert.DoesNotContain(report.Frameworks, item => item.Id == "left-pad");
    }

    [Fact]
    public async Task Pasta_com_nome_de_linguagem_e_python_version_sozinho_nao_viram_stack()
    {
        using var project = new TempProject();
        project.Write("python-scripts/readme.md", "# nada\n");
        project.Write("rust/notes.txt", "nada\n");
        project.Write(".python-version", "3.12\n");
        project.Write("Cargo.toml", "# sem [package] nem [workspace]\n");

        var report = await Discover(project);

        Assert.Empty(report.Stacks);
    }

    [Fact]
    public void O_perfil_traz_os_frameworks_com_categoria_e_a_regra_de_stack_certa()
    {
        var report = new DiscoveryReport("p-aaaaaaaaaaaa", "demo",
            [new StackFinding("node", "Node.js", "", "22", Confidence.Medium, [], []), new StackFinding("python", "Python", "ai", "3.12", Confidence.Medium, [], [])],
            [], [], HarnessInventory.Empty, null!, null!, null!, false, [])
        {
            Frameworks =
            [
                new FrameworkFinding("vitest", "Vitest", "node", "", "3.0.0", [new Evidence("package.json")]),
                new FrameworkFinding("langgraph", "LangGraph", "python", "ai", "1.0.4", [new Evidence("ai/requirements.lock")])
            ]
        };

        var profile = ProfileBuilder.Build(report, [], null, DateTimeOffset.UnixEpoch);

        var vitest = Assert.Single(profile.Technologies, technology => technology.Id == "vitest");
        Assert.Equal(("Testes", Confidence.Medium), (vitest.Category, vitest.Confidence));
        Assert.Equal("Linguagem e runtime", Assert.Single(profile.Technologies, technology => technology.Id == "python").Category);
        Assert.True(Assert.Single(profile.Harness, guidance => guidance.Technology == "Vitest").Covered);
        Assert.False(Assert.Single(profile.Harness, guidance => guidance.Technology == "LangGraph").Covered);
    }

    [Fact]
    public async Task O_proprio_spock_mostra_python_langgraph_rust_e_tauri()
    {
        var root = ContractFiles.Root;
        var files = new TestFilesFactory().Open(root);

        var report = await new ProjectDiscoverer(new InterceptingRunner()).DiscoverAsync("p-aaaaaaaaaaaa", root, files, CancellationToken.None);

        Assert.Contains(report.Stacks, stack => stack.Id == "python" && stack.Root == "intelligence" && stack.Version == "3.12");
        Assert.Contains(report.Stacks, stack => stack.Id == "rust" && stack.Root == "desktop/host/src-tauri");
        Assert.Contains(report.Frameworks, framework => framework.Id == "langgraph");
        Assert.Contains(report.Frameworks, framework => framework.Id == "tauri" && framework.StackId == "rust");
        Assert.Contains(report.Frameworks, framework => framework.Id == "typescript");
    }
}

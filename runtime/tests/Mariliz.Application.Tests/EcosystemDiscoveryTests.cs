using Mariliz.Application.Compatibility;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Tests;

// MV-4: PHP, Ruby, C/C++ e Swift pelos manifestos; Kotlin e o plugin do Android no Gradle; o runtime que asdf, mise, Volta,
// SDKMAN, o toolchain do go.mod e o Gradle Wrapper fixam; Microsoft Agent Framework e Semantic Kernel como frameworks.
public class EcosystemDiscoveryTests
{
    private static async Task<DiscoveryReport> Discover(TempProject project)
    {
        var journey = new Journey(project);
        return await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None);
    }

    private static StackFinding Stack(DiscoveryReport report, string id) => Assert.Single(report.Stacks, stack => stack.Id == id);

    private static FrameworkFinding Framework(DiscoveryReport report, string id) => Assert.Single(report.Frameworks, item => item.Id == id);

    private static RuntimeFinding Runtime(DiscoveryReport report, string technology, string root) =>
        Assert.Single(report.Runtimes, runtime => runtime.TechnologyId == technology && runtime.Root == root);

    [Fact]
    public async Task Php_usa_a_plataforma_do_composer_e_lista_laravel_e_phpunit()
    {
        using var project = new TempProject();
        project.Write("api/composer.json", """
            { "require": { "php": "^8.1", "laravel/framework": "^11.0" }, "require-dev": { "phpunit/phpunit": "^11.2" },
              "config": { "platform": { "php": "8.2.15" } } }
            """);
        project.Write("api/composer.lock", "{}");

        var report = await Discover(project);

        var php = Stack(report, "php");
        Assert.Equal(("api", "8.2.15", Confidence.High), (php.Root, php.Version, php.Confidence));
        Assert.Contains(php.Evidence, item => item.Detail == "config.platform.php=8.2.15");
        Assert.Equal("11.0", Framework(report, "laravel").Version);
        Assert.Equal("11.2", Framework(report, "phpunit").Version);
        Assert.Contains(report.Tooling, tool => tool.Kind == "package-manager" && tool.Name == "composer");
        Assert.Contains(report.Tooling, tool => tool.Kind == "test" && tool.Command == "cd api && vendor/bin/phpunit");
    }

    [Theory]
    [InlineData(".php-version", "8.3.4\n", "8.3.4", Confidence.High)]
    [InlineData(null, null, "7.4", Confidence.Medium)]
    public async Task Sem_plataforma_a_versao_vem_do_php_version_ou_do_minimo_do_require(string? file, string? content, string version, Confidence confidence)
    {
        using var project = new TempProject();
        project.Write("composer.json", """{ "require": { "php": ">=7.4" } }""");
        if (file is not null) project.Write(file, content!);

        var php = Stack(await Discover(project), "php");

        Assert.Equal((version, confidence), (php.Version, php.Confidence));
    }

    [Fact]
    public async Task Ruby_le_o_ruby_version_e_as_gems_do_lock()
    {
        using var project = new TempProject();
        project.Write("Gemfile", "source \"https://rubygems.org\"\nruby \"3.2.2\"\ngem 'rails', '~> 7.1.0'\ngem 'rspec-rails', group: :test\n");
        project.Write("Gemfile.lock", "GEM\n  specs:\n    rails (7.1.3)\n    rspec-rails (6.1.0)\n\nRUBY VERSION\n   ruby 3.2.2p53\n");
        project.Write(".ruby-version", "3.3.0\n");

        var report = await Discover(project);

        var ruby = Stack(report, "ruby");
        Assert.Equal(("3.3.0", Confidence.High), (ruby.Version, ruby.Confidence));
        Assert.Equal("7.1.3", Framework(report, "rails").Version);
        Assert.Contains(report.Tooling, tool => tool.Kind == "test" && tool.Command == "bundle exec rspec");
    }

    [Fact]
    public async Task Ruby_sem_ruby_version_usa_o_lock()
    {
        using var project = new TempProject();
        project.Write("Gemfile", "gem 'sinatra'\n");
        project.Write("Gemfile.lock", "GEM\n  specs:\n    sinatra (4.0.0)\n\nRUBY VERSION\n   ruby 3.4.1p0\n");

        var report = await Discover(project);

        Assert.Equal("3.4.1", Stack(report, "ruby").Version);
        Assert.Equal("4.0.0", Framework(report, "sinatra").Version);
    }

    [Fact]
    public async Task Cpp_usa_o_padrao_da_linguagem_e_ignora_o_cmake_de_subpasta()
    {
        using var project = new TempProject();
        project.Write("CMakeLists.txt", "cmake_minimum_required(VERSION 3.20)\nproject(motor LANGUAGES CXX)\nset(CMAKE_CXX_STANDARD 20)\nadd_subdirectory(src)\n");
        project.Write("src/CMakeLists.txt", "add_library(core core.cpp)\n");

        var cpp = Stack(await Discover(project), "cpp");

        Assert.Equal(("", "C++20"), (cpp.Root, cpp.Version));
        Assert.Contains("CMake 3.20 ou mais novo.", cpp.Notes);
    }

    [Fact]
    public async Task Swift_le_a_versao_das_ferramentas_do_package_swift()
    {
        using var project = new TempProject();
        project.Write("Package.swift", "// swift-tools-version:5.9\nimport PackageDescription\n");

        var report = await Discover(project);

        Assert.Equal("5.9", Stack(report, "swift").Version);
        Assert.Contains(report.Tooling, tool => tool.Kind == "test" && tool.Command == "swift test");
    }

    [Fact]
    public async Task Android_com_kotlin_lista_os_plugins_e_o_gradle_do_wrapper()
    {
        using var project = new TempProject();
        project.Write("app/build.gradle.kts", """
            plugins {
                id("com.android.application") version "8.7.0"
                kotlin("android") version "2.1.0"
            }
            java { toolchain { languageVersion.set(JavaLanguageVersion.of(17)) } }
            """);
        project.Write("app/gradle/wrapper/gradle-wrapper.properties", "distributionUrl=https\\://services.gradle.org/distributions/gradle-8.9-bin.zip\n");

        var report = await Discover(project);

        Assert.Equal("17", Stack(report, "java").Version);
        Assert.Equal("8.7.0", Framework(report, "android-gradle-plugin").Version);
        Assert.Equal("2.1.0", Framework(report, "kotlin").Version);
        Assert.Equal(("Gradle (wrapper)", "8.9"), (Runtime(report, "gradle", "app").Name, Runtime(report, "gradle", "app").Version));
    }

    [Fact]
    public async Task Tool_versions_na_raiz_vale_para_as_pastas_e_o_volta_vence_o_gerenciador()
    {
        using var project = new TempProject();
        project.Write(".tool-versions", "nodejs 20.11.0\npython 3.11.4 3.10.0 # a primeira vale\nruby system\n");
        project.Write("web/package.json", """{ "dependencies": { "react": "^19.3.0" } }""");
        project.Write("admin/package.json", """{ "dependencies": { "react": "^18.3.1" }, "volta": { "node": "18.19.0" } }""");
        project.Write("svc/pyproject.toml", "[project]\nname = \"svc\"\nrequires-python = \">=3.10\"\n");
        project.Write("jobs/Gemfile", "gem 'sidekiq'\n");

        var report = await Discover(project);

        var web = Runtime(report, "node", "web");
        Assert.Equal(("20.11.0", ".tool-versions"), (web.Version, web.Evidence[0].Path));
        var admin = Runtime(report, "node", "admin");
        Assert.Equal(("18.19.0", "volta.node=18.19.0"), (admin.Version, admin.Evidence[0].Detail));
        Assert.Equal("3.11.4", Runtime(report, "python", "svc").Version);
        Assert.DoesNotContain(report.Runtimes, runtime => runtime.TechnologyId == "ruby");
    }

    [Fact]
    public async Task Mise_go_toolchain_e_sdkman_fixam_o_runtime()
    {
        using var project = new TempProject();
        project.Write("mise.toml", "[env]\nFOO = \"1\"\n\n[tools]\nruby = \"3.3.1\"\nnode = [\"22.12.0\", \"20\"]\n");
        project.Write("rb/Gemfile", "gem 'rails'\n");
        project.Write("cli/go.mod", "module example.com/cli\n\ngo 1.22\n\ntoolchain go1.22.5\n");
        project.Write("svc/pom.xml", "<project><properties><maven.compiler.release>17</maven.compiler.release></properties></project>");
        project.Write("svc/.sdkmanrc", "java=17.0.9-tem\n");

        var report = await Discover(project);

        Assert.Equal("3.3.1", Runtime(report, "ruby", "rb").Version);
        Assert.Equal(("Go (toolchain)", "1.22.5"), (Runtime(report, "go", "cli").Name, Runtime(report, "go", "cli").Version));
        Assert.Equal("1.22", Stack(report, "go").Version);
        Assert.Equal("17.0.9", Runtime(report, "java", "svc").Version);
    }

    [Fact]
    public async Task Agent_framework_e_semantic_kernel_aparecem_como_frameworks_de_ia()
    {
        using var project = new TempProject();
        project.Write("Agentes.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>" +
            "<ItemGroup><PackageReference Include=\"Microsoft.Agents.AI.OpenAI\" Version=\"1.28.0\" /></ItemGroup></Project>");
        project.Write("py/pyproject.toml", "[project]\nname = \"py\"\nrequires-python = \">=3.11\"\ndependencies = [\"agent-framework-core>=1.26.0\", \"semantic-kernel==1.35.0\"]\n");

        var report = await Discover(project);

        var agents = report.Frameworks.Where(item => item.Id == "agent-framework").ToDictionary(item => item.StackId, item => item.Version);
        Assert.Equal("1.28.0", agents["dotnet"]);
        Assert.Equal("1.26.0", agents["python"]);
        Assert.Equal("1.35.0", Assert.Single(report.Frameworks, item => item.Id == "semantic-kernel").Version);
    }

    [Fact]
    public async Task Php_e_ruby_ganham_situacao_de_suporte_pelo_catalogo_embutido()
    {
        using var project = new TempProject();
        project.Write("composer.json", """{ "require": { "php": "^7.4" } }""");
        project.Write("rb/Gemfile", "gem 'rails'\n");
        project.Write("rb/.ruby-version", "2.7.8\n");

        var compatibility = CompatibilityAssessor.Assess(ProjectModules.From(await Discover(project)), ReleaseCatalog.Embedded,
            new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(SupportStatuses.EndOfLife, Assert.Single(compatibility.Modules, module => module.TechnologyId == "php").Status);
        Assert.Equal(SupportStatuses.EndOfLife, Assert.Single(compatibility.Modules, module => module.TechnologyId == "ruby").Status);
    }
}

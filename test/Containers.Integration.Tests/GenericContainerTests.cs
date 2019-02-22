using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Containers.Integration.Tests.Fixtures;
using TestContainers.Containers;
using Xunit;

namespace Containers.Integration.Tests
{
    [Collection(GenericContainerTestCollection.CollectionName)]
    public class GenericContainerTests
    {
        private readonly GenericContainerFixture _fixture;

        protected IContainer Container => _fixture.Container;

        public GenericContainerTests(GenericContainerFixture fixture)
        {
            _fixture = fixture;
        }

        public class ExecuteCommandTests : GenericContainerTests
        {
            public ExecuteCommandTests(GenericContainerFixture fixture)
                : base(fixture)
            {
            }

            [Fact]
            public async Task ShouldReturnSuccessfulResponseInStdOut()
            {
                // arrange
                const string hello = "hello-world";

                // act
                var (stdout, stderr) = await Container.ExecuteCommand("echo", hello);

                // assert
                Assert.Equal(hello, stdout.TrimEndNewLine());
                Assert.True(string.IsNullOrEmpty(stderr));
            }

            [Fact]
            public async Task ShouldReturnFailureResponseInStdErr()
            {
                // act
                var (stdout, stderr) = await Container.ExecuteCommand("sh", "echo");

                // assert
                Assert.True(string.IsNullOrEmpty(stdout));
                Assert.False(string.IsNullOrEmpty(stderr));
            }
        }

        public class EnvironmentVariablesTests : GenericContainerTests
        {
            private readonly KeyValuePair<string, string> _injectedEnvVar;

            public EnvironmentVariablesTests(GenericContainerFixture fixture)
                : base(fixture)
            {
                _injectedEnvVar = fixture.InjectedEnvVar;
            }

            [Fact]
            public async Task ShouldBeAvailableWhenTheyAreSet()
            {
                // act
                var (stdout, _) = await Container.ExecuteCommand("sh", "-c", "echo $" + _injectedEnvVar.Key);

                // assert
                Assert.Equal(_injectedEnvVar.Value, stdout.TrimEndNewLine());
            }
        }

        public class ExposedPortsTests : GenericContainerTests
        {
            private readonly int _exposedPort;

            public ExposedPortsTests(GenericContainerFixture fixture)
                : base(fixture)
            {
                _exposedPort = fixture.ExposedPort;
            }

            [Fact]
            public void ShouldBeAvailableWhenTheyAreSet()
            {
                // arrange
                var mappedPort = Container.GetMappedPort(_exposedPort);

                // act
                var tcpClient = new TcpClient("localhost", mappedPort);

                // assert
                Assert.True(tcpClient.Connected);
            }

            [Fact]
            public void ShouldNotBeAbleToConnectToUnexposedPort()
            {
                // act
                var ex = Record.Exception(() => new TcpClient("localhost", _exposedPort));

                // assert
                Assert.IsAssignableFrom<SocketException>(ex);
            }
        }

        public class PortBindingTests : GenericContainerTests
        {
            private readonly KeyValuePair<int, int> _portBinding;

            public PortBindingTests(GenericContainerFixture fixture)
                : base(fixture)
            {
                _portBinding = fixture.PortBinding;
            }

            [Fact]
            public void ShouldBeAvailableWhenTheyAreSet()
            {
                // act
                var tcpClient = new TcpClient("localhost", _portBinding.Value);

                // assert
                Assert.True(tcpClient.Connected);
            }

            [Fact]
            public void ShouldReturnBoundPortWhenGetMappedPortIsCalled()
            {
                // act
                var result = Container.GetMappedPort(_portBinding.Key);

                // assert
                Assert.Equal(_portBinding.Value, result);
            }
        }

        public class CommandTests : GenericContainerTests
        {
            private readonly string _fileTouchedByCommand;

            public CommandTests(GenericContainerFixture fixture)
                : base(fixture)
            {
                _fileTouchedByCommand = fixture.FileTouchedByCommand;
            }

            [Fact]
            public async Task ShouldRunCommandWhenContainerStarts()
            {
                // act
                var (stdout, _) = await Container.ExecuteCommand("/bin/sh", "-c",
                    $"if [ -e {_fileTouchedByCommand} ]; then echo 1; fi");

                // assert
                Assert.Equal("1", stdout.TrimEndNewLine());
            }
        }

        public class WorkingDirectoryTests : GenericContainerTests
        {
            private readonly string _workingDirectory;

            public WorkingDirectoryTests(GenericContainerFixture fixture)
                : base(fixture)
            {
                _workingDirectory = fixture.WorkingDirectory;
            }

            [Fact]
            public async Task ShouldSetWorkingDirectoryWhenContainerStarts()
            {
                // act
                var (stdout, _) = await Container.ExecuteCommand("pwd");

                // assert
                Assert.Equal(_workingDirectory, stdout.TrimEndNewLine());
            }
        }

        public class PrivilegedModeTests : GenericContainerTests
        {
            public PrivilegedModeTests(GenericContainerFixture fixture)
                : base(fixture)
            {
            }

            // todo: test for privileged mode with privileged container
            [Fact]
            public async Task ShouldFailToRunPrivilegedOperations()
            {
                // act
                var (stdout, stderr) = await Container.ExecuteCommand("ip", "link", "add", "dummy0", "type", "dummy");

                // assert
                Assert.NotEmpty(stderr);
                Assert.Empty(stdout);
            }
        }

        public class BindMountTests : GenericContainerTests
        {
            private readonly KeyValuePair<string, string> _hostPathBinding;

            public BindMountTests(GenericContainerFixture fixture)
                : base(fixture)
            {
                _hostPathBinding = fixture.HostPathBinding;
            }

            [Fact]
            public async Task ShouldContainFileCreatedInHostBindingDirectory()
            {
                // arrange
                var content = Guid.NewGuid().ToString();
                const string filename = "my_file";
                var filepath = Path.Combine(_hostPathBinding.Key, filename);
                File.WriteAllText(filepath, content);

                // act
                var (stdout, stderr) =
                    await Container.ExecuteCommand("cat", Path.Combine(_hostPathBinding.Value, filename));

                // assert
                Assert.Equal(content, stdout.TrimEndNewLine());
            }
        }
    }
}
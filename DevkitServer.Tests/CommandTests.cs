using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;
using DevkitServer.Commands.Subsystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.Tests;

/// <summary>Tests for <see cref="CommandParser"/>.</summary>
[TestClass]
public class CommandTests
{
    private readonly TestCommand[] _sampleCommands =
    {
        new TestCommand(-1, "action", "a"),
        new TestCommand(1, "action", "action2"),
        new TestCommand(0, "help", "hlp", "h")
    };

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TestParseCommandNoArgumentsLen1(bool slash)
    {
        MockRunCommand("a", slash, out IExecutableCommand command, out _);

        Assert.AreEqual(_sampleCommands[0], command);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TestParseCommand1ArgumentLen1(bool slash)
    {
        MockRunCommand("a test", slash, out IExecutableCommand command, out string[] args);

        Assert.AreEqual(_sampleCommands[0], command);
        Assert.AreEqual(1, args.Length);
        Assert.AreEqual("test", args[0]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TestParseCommandNoArguments(bool slash)
    {
        MockRunCommand("help", slash, out IExecutableCommand command, out _);

        Assert.AreEqual(_sampleCommands[2], command);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TestParseCommandArguments(bool slash)
    {
        MockRunCommand("help action please", slash, out IExecutableCommand command, out string[] args);

        Assert.AreEqual(_sampleCommands[2], command);
        Assert.AreEqual(2, args.Length);
        Assert.AreEqual("action", args[0]);
        Assert.AreEqual("please", args[1]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TestParseCommandArgumentsQuotations(bool slash)
    {
        MockRunCommand("help \"action please\"", slash, out IExecutableCommand command, out string[] args);

        Assert.AreEqual(_sampleCommands[2], command);
        Assert.AreEqual(1, args.Length);
        Assert.AreEqual("action please", args[0]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TestParseCommandArgumentsUnendedQuotations(bool slash)
    {
        MockRunCommand("help \"action please", slash, out IExecutableCommand command, out string[] args);

        Assert.AreEqual(_sampleCommands[2], command);
        Assert.AreEqual(1, args.Length);
        Assert.AreEqual("action please", args[0]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TestParseCommandArgumentsOddQuotations(bool slash)
    {
        MockRunCommand("help \"action please\" fr\"", slash, out IExecutableCommand command, out string[] args);

        Assert.AreEqual(_sampleCommands[2], command);
        Assert.AreEqual(2, args.Length);
        Assert.AreEqual("action please", args[0]);
        Assert.AreEqual("fr", args[1]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TestParseCommandArgumentsQuotationsMissingSpace(bool slash)
    {
        MockRunCommand("help\"action please\"", slash, out IExecutableCommand command, out string[] args);

        Assert.AreEqual(_sampleCommands[2], command);
        Assert.AreEqual(1, args.Length);
        Assert.AreEqual("action please", args[0]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TestParseCommandArgumentsSpacing(bool slash)
    {
        MockRunCommand("help     me   \"please help with /action\"please", slash, out IExecutableCommand command, out string[] args);

        Assert.AreEqual(_sampleCommands[2], command);
        Assert.AreEqual(3, args.Length);
        Assert.AreEqual("me", args[0]);
        Assert.AreEqual("please help with /action", args[1]);
        Assert.AreEqual("please", args[2]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TestParseCommandAdjacentQuotationArguments(bool slash)
    {
        MockRunCommand("help \"please help with /action\"\"like seriously i need help with this\"", slash, out IExecutableCommand command, out string[] args);

        Assert.AreEqual(_sampleCommands[2], command);
        Assert.AreEqual(2, args.Length);
        Assert.AreEqual("please help with /action", args[0]);
        Assert.AreEqual("like seriously i need help with this", args[1]);
    }
    
    private void MockRunCommand(string cmd, bool slash, out IExecutableCommand command, out string[] args)
    {
        if (slash)
            cmd = "/" + cmd;
        IExecutableCommand? command2 = null;
        string[]? args2 = null;
        if (ThreadUtil.gameThread == null)
            ThreadUtil.setupGameThread();
        TestCommandHandler handler = new TestCommandHandler(_sampleCommands);
        CommandParser parser = new CommandParser(handler);
        handler.OnCommandExecuted +=
#if SERVER
            (_, context) =>
#elif CLIENT
            context =>
#endif
        {
            command2 = context.Command;
            args2 = context.Arguments;
        };
        bool shouldList = true;
        Assert.IsTrue(parser.TryRunCommand(
#if SERVER
            null,
#elif CLIENT
            false,
#endif
            cmd, ref shouldList, slash));
        Assert.IsNotNull(command2);
        Assert.IsNotNull(args2);
        command = command2;
        args = args2;
    }

    private class TestCommandHandler : ICommandHandler
    {
        public IReadOnlyList<IExecutableCommand> Commands { get; }
        public event CommandHandler.ExecutedCommand? OnCommandExecuted;
        public event CommandHandler.ExecutingCommand? OnExecutingCommand;
        public event Action<IExecutableCommand>? OnCommandRegistered;
        public event Action<IExecutableCommand>? OnCommandDeregistered;

        public TestCommandHandler(params TestCommand[] commands)
        {
            Commands = commands;
        }
        public void Init() => throw new NotImplementedException();
        public void ExecuteCommand(IExecutableCommand command,
#if SERVER
            EditorUser? user, 
#elif CLIENT
            bool console,
#endif
            string[] args, string originalMessage)
        {
            OnCommandExecuted?.Invoke(
#if SERVER
                user, 
#endif
                new CommandContext(command, args, originalMessage,
#if SERVER
                    user
#elif CLIENT
                    console
#endif
                    ));
        }
        public void SendHelpMessage(
#if SERVER
            EditorUser? user
#elif CLIENT
            bool console
#endif
            ) => throw new NotImplementedException();
        public void SendNoPermissionMessage(
#if SERVER
            EditorUser? user, 
#elif CLIENT
            bool console,
#endif
            IExecutableCommand command) => throw new NotImplementedException();
        public bool TryRegisterCommand(IExecutableCommand command) => throw new NotImplementedException();
        public bool TryDeregisterCommand(IExecutableCommand command) => throw new NotImplementedException();
        public void HandleCommandException(CommandContext ctx, Exception ex) => throw new NotImplementedException();
#if CLIENT
        public void TransitionCommandExecutionToServer(CommandContext ctx) => throw new NotImplementedException();
#endif
    }
    [API.Ignore]
    private class TestCommand : IExecutableCommand
    {
        public string CommandName { get; }
        public IList<string> Aliases { get; }
        public int Priority { get; }
        public TestCommand(int priority, string name, params string[] aliases)
        {
            Priority = priority;
            CommandName = name;
            Aliases = aliases;
        }
        public IDevkitServerPlugin? Plugin
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
        public IList<Permission> Permissions => throw new NotImplementedException();
        public bool Asynchronous => throw new NotImplementedException();
        public bool AnyPermissions => throw new NotImplementedException();
        public void Execute(CommandContext ctx) => throw new NotImplementedException();
        public Task ExecuteAsync(CommandContext ctx, CancellationToken token) => throw new NotImplementedException();
        public bool CheckPermission(
#if SERVER
            EditorUser? user
#endif
            ) => throw new NotImplementedException();
        public override string ToString() => CommandName + " (" + string.Join("|", Aliases) + ") Priority: " + Priority;
    }
}
using System.CommandLine;
using AzBoardCodexTool.Services;

namespace AzBoardCodexTool.Commands;

public static class WorkItemCommands
{
    public static RootCommand CreateRootCommand(AzureBoardsService service)
    {
        var root = new RootCommand(
            "CLI reusable para crear, actualizar, consultar y relacionar Work Items en Azure Boards.");

        root.Subcommands.Add(CreateCreateCommand(service));
        root.Subcommands.Add(CreateUpdateCommand(service));
        root.Subcommands.Add(CreateGetCommand(service));
        root.Subcommands.Add(CreateLinkParentCommand(service));
        root.Subcommands.Add(CreateQueryCommand(service));
        return root;
    }

    private static Command CreateCreateCommand(AzureBoardsService service)
    {
        var type = RequiredStringOption(
            "--type",
            "Tipo: Epic, Feature, User Story, Product Backlog Item, Task o Bug.");
        var title = RequiredStringOption("--title", "Título del Work Item.");
        var description = OptionalStringOption("--description", "Descripción del Work Item.");
        var assignedTo = OptionalStringOption(
            "--assigned-to",
            "Usuario asignado (nombre visible, correo o identidad reconocida por Azure DevOps).");
        var acceptanceCriteria = OptionalStringOption(
            "--acceptance-criteria",
            "Criterios de aceptación del Work Item.");
        var tags = OptionalStringOption("--tags", "Tags separados por punto y coma.");
        var comment = OptionalStringOption("--comment", "Comentario inicial del Work Item.");
        var attachments = OptionalStringArrayOption(
            "--attachment",
            "Ruta de archivo para adjuntar. Puede repetirse.");

        var command = new Command("create", "Crea un Work Item.");
        command.Options.Add(type);
        command.Options.Add(title);
        command.Options.Add(description);
        command.Options.Add(assignedTo);
        command.Options.Add(acceptanceCriteria);
        command.Options.Add(tags);
        command.Options.Add(comment);
        command.Options.Add(attachments);

        command.SetAction(async (parseResult, cancellationToken) =>
            await ConsoleOutput.ExecuteAsync(async () =>
            {
                var item = await service.CreateAsync(
                    parseResult.GetRequiredValue(type),
                    parseResult.GetRequiredValue(title),
                    parseResult.GetValue(description),
                    parseResult.GetValue(assignedTo),
                    parseResult.GetValue(acceptanceCriteria),
                    parseResult.GetValue(tags),
                    parseResult.GetValue(comment),
                    parseResult.GetValue(attachments),
                    cancellationToken);
                ConsoleOutput.PrintCreated(item);
            }));

        return command;
    }

    private static Command CreateUpdateCommand(AzureBoardsService service)
    {
        var id = RequiredIntOption("--id", "ID del Work Item.");
        var title = OptionalStringOption("--title", "Nuevo título.");
        var description = OptionalStringOption("--description", "Nueva descripción.");
        var assignedTo = OptionalStringOption(
            "--assigned-to",
            "Nuevo usuario asignado (nombre visible, correo o identidad reconocida por Azure DevOps).");
        var acceptanceCriteria = OptionalStringOption(
            "--acceptance-criteria",
            "Nuevos criterios de aceptación del Work Item.");
        var state = OptionalStringOption("--state", "Nuevo estado.");
        var tags = OptionalStringOption("--tags", "Nuevos tags separados por punto y coma.");
        var comment = OptionalStringOption("--comment", "Comentario para agregar al Work Item.");
        var attachments = OptionalStringArrayOption(
            "--attachment",
            "Ruta de archivo para adjuntar. Puede repetirse.");

        var command = new Command("update", "Actualiza campos de un Work Item.");
        command.Options.Add(id);
        command.Options.Add(title);
        command.Options.Add(description);
        command.Options.Add(assignedTo);
        command.Options.Add(acceptanceCriteria);
        command.Options.Add(state);
        command.Options.Add(tags);
        command.Options.Add(comment);
        command.Options.Add(attachments);

        command.SetAction(async (parseResult, cancellationToken) =>
            await ConsoleOutput.ExecuteAsync(async () =>
            {
                var item = await service.UpdateAsync(
                    parseResult.GetRequiredValue(id),
                    parseResult.GetValue(title),
                    parseResult.GetValue(description),
                    parseResult.GetValue(assignedTo),
                    parseResult.GetValue(acceptanceCriteria),
                    parseResult.GetValue(state),
                    parseResult.GetValue(tags),
                    parseResult.GetValue(comment),
                    parseResult.GetValue(attachments),
                    cancellationToken);
                ConsoleOutput.PrintUpdated(item, "Updated");
            }));

        return command;
    }

    private static Command CreateGetCommand(AzureBoardsService service)
    {
        var id = RequiredIntOption("--id", "ID del Work Item.");
        var command = new Command("get", "Consulta un Work Item por ID.");
        command.Options.Add(id);

        command.SetAction(async (parseResult, cancellationToken) =>
            await ConsoleOutput.ExecuteAsync(async () =>
            {
                var item = await service.GetAsync(
                    parseResult.GetRequiredValue(id),
                    cancellationToken);
                ConsoleOutput.PrintJson(item);
            }));

        return command;
    }

    private static Command CreateLinkParentCommand(AzureBoardsService service)
    {
        var childId = RequiredIntOption("--child-id", "ID del Work Item hijo.");
        var parentId = RequiredIntOption("--parent-id", "ID del Work Item padre.");

        var command = new Command(
            "link-parent",
            "Relaciona un Work Item hijo con un Work Item padre.");
        command.Options.Add(childId);
        command.Options.Add(parentId);

        command.SetAction(async (parseResult, cancellationToken) =>
            await ConsoleOutput.ExecuteAsync(async () =>
            {
                var child = await service.LinkParentAsync(
                    parseResult.GetRequiredValue(childId),
                    parseResult.GetRequiredValue(parentId),
                    cancellationToken);
                ConsoleOutput.PrintUpdated(child, "Linked");
            }));

        return command;
    }

    private static Command CreateQueryCommand(AzureBoardsService service)
    {
        var wiql = RequiredStringOption("--wiql", "Consulta WIQL.");
        var command = new Command("query", "Lista Work Items usando WIQL.");
        command.Options.Add(wiql);

        command.SetAction(async (parseResult, cancellationToken) =>
            await ConsoleOutput.ExecuteAsync(async () =>
            {
                var items = await service.QueryAsync(
                    parseResult.GetRequiredValue(wiql),
                    cancellationToken);
                ConsoleOutput.PrintQueryResults(items);
            }));

        return command;
    }

    private static Option<string> RequiredStringOption(string name, string description) =>
        new(name) { Description = description, Required = true };

    private static Option<string?> OptionalStringOption(string name, string description) =>
        new(name) { Description = description };

    private static Option<string[]> OptionalStringArrayOption(string name, string description) =>
        new(name) { Description = description, Arity = ArgumentArity.ZeroOrMore };

    private static Option<int> RequiredIntOption(string name, string description) =>
        new(name) { Description = description, Required = true };
}

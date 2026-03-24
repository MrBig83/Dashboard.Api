namespace Dashboard.Api.Models;

public record SchoolMenuDay(string Day, string[] Items);

public record SchoolMenuResponse(SchoolMenuDay[] Days);

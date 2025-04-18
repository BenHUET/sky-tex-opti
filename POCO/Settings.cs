namespace sky_tex_opti.POCO;

public record Settings(
    Target[] Targets, 
    Exclusions Exclusions
);

public record Exclusions(
    string[] Filenames, 
    string[] Paths
);

public record Target(
    string[] Suffixes, 
    int Resolution
);


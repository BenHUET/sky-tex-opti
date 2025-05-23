## Description
Simple software to optimize ModOrganizer 2 modlists for Skyrim by resizing textures. Inspired by VRAMr but it's far faster, more configurable and also cross-platform.

## Quickstart
- Download the archive for your platform
- Unzip it and open a command line/terminal inside the extracted folder

Windows:
```
sky-tex-opti.exe --profile "C:\path\to\MO2\modlist\profile" --output "C:\where\to\write\output" --settings default.json
```

Linux:
```
./sky-tex-opti --profile "/path/to/MO2/modlist/profile" --output "/path/where/to/write/output" --settings default.json
```

I you want to use the `performance` settings instead, replace `default.json` by `performance.json`. 

- Wait for it to complete
- Open MO2
- Create a new empty mod (name doesn't matter)
- Place it at the end of your list
- Copy the `textures` folder from the output folder inside the mod folder you just created
- Enable the new mod

## Usage
```
--profile      Required. Path to the MO2 profile to optimize. 
               Mutually exclusive with --modlist.

--modlist      Required. Path to a custom modlist : newline-separated list of absolute paths to mods.
               Mutually exclusive with --profile.

--output       Required. Path where to store the output.

--settings     (Default: default.json) Path to the settings file.

--resume       Will use an existing output folder and process only missing files.
```

## Settings
Settings files are JSON files instructing the software which textures should be optimized and how to.

It's easy to make your own and use it with `--settings new_settings.json`.

Provided settings file :
- `default.json` : 2k diffuse, 1024 for normals, 512 for the rest
- `performance.json` : 2k diffuse, 1024 for the rest
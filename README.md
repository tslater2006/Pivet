# Introduction

Pivet allows for git-based version control for PeopleSoft definitions. It has built in support for a variety of definition types as well as it allows you to configure specific tables to be version controlled.

Currently Pivet natively supports version controlling the following definitions:
 - HTML Objects
 - Message Catalog Entries
 - PeopleCode
 - Content References
 - SQL Objects
 - Stylesheets
 - Translate Values

In addition to these, Pivet also supports tracking raw peopletools tables using the Raw Data Processor. See [Using RawDataProcessor](docs/raw_data.md) for more information on how to configure Raw Data entries.

## Getting Started

These instructions will get you a copy of the project up and running on your local machine for development and testing purposes. See deployment for notes on how to deploy the project on a live system.

### Installing

Pivet is built using .NET Core which means it should run on a variety of systems (Windows,Linux,MacOS). 

Release binaries are provided for Windows/Linux/MacOS/RedHat, as both regular builds which require the .NET Core framework to be installed on the host and as self-contained builds which do not require the .NET Core to be installed.

In most cases it is sufficient to grab the self-contained build for your platform, extract it somewhere and execute the Pivet binary for your platform. Please note that you will need a Configuration file to configure Pivet to version the objects you want. 

If you would like to build Pivet from source we currently have build instructions for Linux, please visit the [Linux build document](Linux.md). This document explains how to build Pivet for a variety of distributions.

### Configuration

In order to use Pivet you must create a configuration file. By default Pivet expects to find `config.json` in the same directory as the executable, but this can be placed elsewhere and given to Pivet on the command line with the `-c` flag.

The Pivet config consist of 3 sections:

* Environments
* Profiles
* Jobs

Environments specify connection details to the target database.

Profiles tell Pivet what objects to version control

Jobs pair up a profile to an environment and contain the local working directory path and Git repository information

For complete documentation of the configuration file format please see [Configuration File Format](docs/config_format.md)

There is also a [sample configuration file](samples/sample-config.json) to use as a starting place.

### Running Pivet

Excuting the Pivet binary with no parameters will cause it to load the config.json file and execute all jobs listed in the configuration. Pivet supports various flags which can impact the behavior:

* `-c <filepath>`
  * Location for `config.json` file to use
  * Example: `-c /home/tim/config.json`
* `-vars <filepath>`
  * Location of the variables file to use
  * For how to use the variables file please see [Configuration File Format](docs/config_format.md)
* `-j <job>`
  * Runs only the job specified
* `-v`
  * Pivet will provide progress indicators 
* `-m`
  * Specifies a custom commit message to use for this run only
* `-b`
  * Runs the configuration builder, a guided way to create the configuration file.
* `-e`
  * Runs Pivet in "encrypt" mode, a special mode used to encrypt passwords which can then be placed in config.json.

## How it works

When Pivet is processing a job, the following sequence of events occurs:


1. Repository on Disk is initialized
2. Working directory of the repository is deleted
3. Objects are extracted from the database and written to file
4. Changes reported by Git are grouped and commited to the repository
5. Repository is pushed to remote

During the first step of repository initialization, Pivet will check the local path and see if the repository already exists. If it does not exist, Pivet will attempt to clone the remote repository to disk. If there is no repository at the remote (or remote isn't configured), Pivet will create a fresh repository on disk and initialize it with a `.gitattributes` file to hand line ending conversions.

The next phase clears all files out of the working directory. This allows Pivet to identify items that used to exist but no longer do (ie, were deleted from the database).

Once the working directory is cleared, Pivet begins running its `Processors` which extract and write to disk the supported definitions. Pivet will only run the Processors that are listed in the Profile for the current Job.

After all processors finish, Pivet will then query Git on the status of the repository. Pivet will collect all new files/changed files and all deleted files and group them according to configuration. These changes will then be commited to the local repository.

After all commits have been made the repository is then pushed to the remote (if one is configured)

## Built With

[LibGit2Sharp](https://github.com/libgit2/libgit2sharp)

[Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)

[Portable.BouncyCastle](https://github.com/onovotny/bc-csharp)


## Contributing

If you find an issue with Pivet please consider opening an issue on the issue tracker. I am also open to pull requests for contributions to the utility.

## Authors

Pivet was written by Tim Slater from [IntraSee](https://intrasee.com/).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details


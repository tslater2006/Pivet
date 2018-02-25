# Running on Linux

A few notes on how to get started with DotNet on Linux to run Pivet.
This was run on RedHat 7.5 Beta, so you will likely find differences.

## Installing Prerequisites

Following the [RedHat documentation](https://access.redhat.com/documentation/en-us/net_core/2.0/html/getting_started_guide/gs_install_dotnet),
you will need to enable DotNet support in `vi /etc/yum.repos.d/redhat.repo`
by setting:

```
[rhel-7-server-dotnet-rpms]
enabled = 1
```

Then install the packages:
```
yum update
yum -y install scl-utils rh-dotnet20 git
```

Then follow the [Mono project documentation](http://www.mono-project.com/download/stable/#download-lin-centos)
to install those prerequisites:

```
rpm --import "http://keyserver.ubuntu.com/pks/lookup?op=get&search=0x3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
su -c 'curl https://download.mono-project.com/repo/centos7-stable.repo | tee /etc/yum.repos.d/mono-centos7-stable.repo'
yum -y install mono-devel
```

Then enable dotnet support in your session (this modifies your PATH to include
dotnet20): `scl enable rh-dotnet20 bash`

Then clone Pivet from GitHub like usual.

Within the `Pivet/Pivet` directory, run:

```
dotnet build -f netcoreapp2.0
dotnet run -f netcoreapp2.0
```

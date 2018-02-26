# Running on Linux

A few notes on how to get started with DotNet on Linux to run Pivet.
This was run on RedHat 7.5 Beta, so you will likely find differences.
## RedHat
### Installing Prerequisites

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

### Building Pivet
Then clone Pivet from GitHub like usual.

Within the `Pivet/Pivet` directory, run:

```
dotnet build -f netcoreapp2.0
dotnet run -f netcoreapp2.0
```
## Ubuntu 16.04

### Installing Pre-Requisites
Add Microsoft's GPG key to the list of trusted keys
```
sudo apt-get install -y curl git
curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg
sudo mv microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg
```
Then add their repository to apt sources
```
sudo sh -c 'echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-xenial-prod xenial main" > /etc/apt/sources.list.d/dotnetdev.list'
```
Finally run apt update and install dotnet
```
sudo apt update && sudo apt-get install dotnet-sdk-2.1.4
```

### Building Pivet
First clone the repository
```
git clone https://github.com/tslater2006/Pivet.git
```
Go to the Pivet project folder
```
cd Pivet/Pivet
```
Restore pacakges and run a release build
```
dotnet restore
dotnet build -c Release -f netcoreapp2.0 -r linux-x64
```
### Executable setup
Create a directory to hold the Pivet executable

```
sudo mkdir /etc/Pivet
```
Copy the build results
```
cp bin/Release/netcoreapp2.0/linux-x64/* /etc/Pivet/
```
Modify permissions
```
sudo chmod 755 /etc/Pivet/*
```
Add a symlink to /usr/bin
```
sudo ln -s /etc/Pivet/Pivet /usr/bin/Pivet
```
At this point you should be able to run `Pivet` from anywhere

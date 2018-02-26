# Running on Linux

Depending on the distrobution you are running, the build instructions can differ. 

Currently there are build instructions for RedHat, Ubuntu, and Oracle Linux.

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

Then enable dotnet support in your session (this modifies your PATH to include
dotnet20): `scl enable rh-dotnet20 bash`

### Building Pivet
Then clone Pivet from GitHub like usual.

Within the `Pivet/Pivet` directory, run:

```
dotnet restore
dotnet build -c Release -f netcoreapp2.0 -r linux-x64
```

### Rebuild LibGit2 Native
TBD

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
sudo mkdir /opt/Pivet
```
Copy the build results
```
cp bin/Release/netcoreapp2.0/linux-x64/* /opt/Pivet/
```
Modify permissions
```
sudo chmod 755 /opt/Pivet/*
```
Add a symlink to /usr/bin
```
sudo ln -s /opt/Pivet/Pivet /usr/bin/Pivet
```
At this point you should be able to run `Pivet` from anywhere

## Oracle Linux 7

### Install Pre-Requisites
Add Microsoft's GPG Key:
```
sudo rpm --import https://packages.microsoft.com/keys/microsoft.asc
udo sh -c 'echo -e "[packages-microsoft-com-prod]\nname=packages-microsoft-com-prod \nbaseurl=https://packages.microsoft.com/yumrepos/microsoft-rhel7.3-prod\nenabled=1\ngpgcheck=1\ngpgkey=https://packages.microsoft.com/keys/microsoft.asc" > /etc/yum.repos.d/dotnetdev.repo'
```

Install required software:
```
sudo yum update
sudo yum install libunwind libicu git cmake libcurl gcc 
sudo yum install dotnet-sdk-2.0.0
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

### Rebuild LibGit2 Native
In a working directory, clone the LibGit2Sharp Native Binaries repository and build
```
git clone --recursive https://github.com/libgit2/libgit2sharp.nativebinaries.git
cd libgit2sharp.nativebinaries/
./build.libgit2.sh
```

Now find the new libgit2*.so file
```
cd nuget.package/libgit2/linux-x64/native/
ls libgit2*.so
pwd
```
For the next set of instructions, assume the .so file name is `libgit2-15e1193.so` and has the path
`/home/vagrant/libgit2sharp.nativebinaries/nuget.package/libgit2/linux-x64/native/`

Navigate to your .nuget directory and copy the new .so file over the existing
```
cd ~/.nuget/packages/libgit2sharp.nativebinaries/1.0.192/runtimes/linux-x64/native
cp /home/vagrant/libgit2sharp.nativebinaries/nuget.package/libgit2/linux-x64/native/libgit2-15e1193.so .
```

### Executable setup
Create a directory to hold the Pivet executable

```
sudo mkdir /opt/Pivet
```
Copy the build results
```
cp bin/Release/netcoreapp2.0/linux-x64/* /opt/Pivet/
```
Modify permissions
```
sudo chmod 755 /opt/Pivet/*
```
Add a symlink to /usr/bin
```
sudo ln -s /opt/Pivet/Pivet /usr/bin/Pivet
```
At this point you should be able to run `Pivet` from anywhere

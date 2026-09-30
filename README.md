# Arcane Motion

Arcane Motion is a Unity VR project being developed using the Unity VR template.

## Project Setup

The project currently uses:

- **Unity:** 6000.6.3f1
- **Template:** VR
- **Repository:** https://github.com/UNO-VR/Arcane-Motion
- **Git LFS:** Used for large/binary project assets

It is recommended that everyone use the same Unity editor version to avoid unnecessary project, package, or asset changes.

## Initial Setup

### 1. Install Unity

Install **Unity 6000.6.3f1** through Unity Hub.

Make sure any platform modules you need for development are installed as well.

### 2. Install Git LFS

This repository uses Git Large File Storage ([Git LFS](https://git-lfs.com/)) for binary assets such as models, textures, audio, and fonts.

Verify that Git LFS is installed:

```bash
git lfs version
```

Then initialize it for your Git installation:

```bash
git lfs install
```

If `git lfs` is not recognized, install Git LFS before cloning the project.

### 3. Clone the Repository

Clone the repository normally:

```bash
git clone https://github.com/UNO-VR/Arcane-Motion.git
```

Then enter the project directory:

```bash
cd Arcane-Motion
```

With Git LFS installed, LFS-managed files should be downloaded automatically during the clone.

If necessary, they can be retrieved manually with:

```bash
git lfs pull
```

### 4. Add the Project to Unity Hub

Open **Unity Hub**.

From the **Add** dropdown, select:

**Add project from disk**

Select the root `Arcane-Motion` repository folder.

The folder should contain directories such as:

```text
Assets/
Packages/
ProjectSettings/
```

Unity Hub should recognize it as a Unity project.

### 5. Open the Project

Open the project using **Unity 6000.6.3f1**.

The first launch will take longer than usual because Unity needs to generate local project files and import the project's assets.

Unity will automatically generate directories and files such as:

```text
Library/
Logs/
Obj/
Temp/
UserSettings/
```

These are local/generated files and are intentionally excluded from Git. They do not need to be downloaded from or committed to GitHub.

## Git and GitHub Authentication

GitHub authentication is required to clone, pull, and push changes to the repository.

If you use **GitHub Desktop**, sign in to your GitHub account through the app. No additional authentication setup is usually required.

If you use Git from the command line, authenticate with GitHub using either:

- `gh auth login` with GitHub CLI
- SSH
- another supported Git credential helper

## Git LFS

Git LFS is configured through the repository's `.gitattributes` file.

Large or binary assets that cannot be usefully diffed by Git are stored using Git LFS, while Unity's text-serialized files remain normal Git files.

To see which file patterns are currently tracked by Git LFS:

```bash
git lfs track
```

To see the LFS files currently present in the repository:

```bash
git lfs ls-files
```

## Files That Should Be Committed

The main Unity project directories that should be kept in Git include:

```text
Assets/
Packages/
ProjectSettings/
```

Unity `.meta` files should also be committed. These files contain GUIDs that Unity uses to maintain references between assets.

Do not manually delete or exclude `.meta` files associated with project assets.

## Files That Should Not Be Committed

Generated Unity files are excluded through `.gitignore`, including directories such as:

```text
Library/
Temp/
Obj/
Logs/
UserSettings/
Build/
Builds/
```

These files are generated locally by Unity and should not be added to the repository.
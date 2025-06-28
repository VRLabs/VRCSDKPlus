<div align="center">

# VRCSDK+

[![Generic badge](https://img.shields.io/github/downloads/VRLabs/VRCSDKPlus/total?label=Downloads)](https://github.com/VRLabs/VRCSDKPlus/releases/latest)
[![Generic badge](https://img.shields.io/badge/License-GPL--3.0-informational.svg)](https://github.com/VRLabs/VRCSDKPlus/blob/main/LICENSE)
[![Generic badge](https://img.shields.io/badge/Unity-2019.4.31f1-lightblue.svg)](https://unity3d.com/unity/whats-new/2019.4.31)
[![Generic badge](https://img.shields.io/badge/SDK-AvatarSDK3-lightblue.svg)](https://vrchat.com/home/download)

[![Generic badge](https://img.shields.io/discord/706913824607043605?color=%237289da&label=DISCORD&logo=Discord&style=for-the-badge)](https://discord.vrlabs.dev/)
[![Generic badge](https://img.shields.io/endpoint.svg?url=https%3A%2F%2Fshieldsio-patreon.vercel.app%2Fapi%3Fusername%3Dvrlabs%26type%3Dpatrons&style=for-the-badge)](https://patreon.vrlabs.dev/)

A set of alternative custom inspectors for the VRCSDK.

![VRCSDK+](https://i.imgur.com/0GLvvGe.gif)

### ⬇️ [Download Latest Version](https://github.com/VRLabs/VRCSDKPlus/releases/latest)


### 📦 [Add to VRChat Creator Companion](https://vrlabs.dev/packages?package=dev.vrlabs.vrcsdkplus)

</div>

---

## What it does

VRCSDK+ provides alternative custom inspectors to the native VRCSDK expression menu and parameters for better functionality and quality of life features.

You can revert to original editors using the context menu of the respective object.  

## What it does not
VRCSDK+ does not modify the VRCSDK in any way.

VRCSDK+ does not require a VRC+ subscription.  

## Features

VRCSDK+ currently provides the following:
### Custom VRCExpression Parameters Inspector
  - Dropdown of parameters available in playable controllers to select easily
  - Ability to add parameters to playable controllers from the editor
  - Warnings and Cleanup to make sure all parameters are valid.
  - Ability to merge two expression parameters
  - Re-orderable list with easy deletion
  - Dynamic, Clean and compact UI

### Custom VRCExpression Menu Inspector (In Collab with @foxscore)
  - Menu History to switch between previously visited Menus
  - QoL buttons such as Copy, Paste, Duplicate and Move
  - Ability to add parameters to expression parameters from the editor
  - Ability to quickly add a new SubMenu asset to SubMenu control
  - Ability to set styling for the control such as Bold, Italic and Color
  - Toggle Compact Mode through the window's options
  - Warnings to make sure all parameters are valid.
  - Dynamic, Clean and compact UI

### Adds "Quick Setup" Context Menu button to VRC Avatar Descriptor 
  - Sets View Position based on Eye bones
  - Triggers "Auto-Detect" for lipsync
  - Sets Eye bones and sets a few defaults for rotation states
  - Automatically sets Eyelids type, Mesh and Blink if found


![](https://i.imgur.com/Lkp5qiI.gif)|![](https://i.imgur.com/Ysg8klM.gif)
:-------------------------:|:-------------------------:
![](https://i.imgur.com/mLncXr1.gif)|![](https://i.imgur.com/7FjUgUG.png)

## For repo contributors
The source code was only recently uploaded. It previously was being built as a DLL with obfuscation which for a few reasons discouraged separating code into files. This made the code kind of disorganized. Apologies for that!

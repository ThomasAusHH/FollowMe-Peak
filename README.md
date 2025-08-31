# FollowMe-Peak

A BepInEx plugin for Peak that automatically records paths in levels and displays them as visual guides for other players.

## Features

- **Automatic Path Recording**: Automatically records your movements in each level
- **Visual Path Display**: Shows recorded paths as visible lines in the game
- **Modern UI**: Clean interface that matches Peak's visual style
- **Difficulty Tracking**: Tracks and saves ascent levels for each climb
- **Advanced Filtering**: Easy search and filter climbs through the UI
- **Biome-specific Paths**: Saves paths separately by biomes/level areas
- **Cloud Synchronization**: Share your paths with other players via an optional server
- **Easy Controls**: F1 key opens the settings menu
- **Persistent Storage**: Paths are saved between game sessions

## Installation

1. Install [BepInEx](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/) via the mod manager
2. Install FollowMe-Peak via your mod manager
3. Start the game

## Usage

- The plugin automatically starts path recording when a level is loaded
- Press **F1** to open/close the settings menu
- In the menu you can:
  - Toggle path visibility on/off
  - Filter climbs by difficulty (Ascent level) / Biome / Climb Code
  - Search and manage climbs easily
  - Configure cloud synchronization
  - Delete paths
- Paths are automatically saved when you light a campfire

## Cloud Synchronization (Optional)

The plugin supports an optional server for sharing paths:
- Upload paths to share them with others
- Download paths from other players
- Automatic synchronization on level start

Server setup is optional and the plugin works completely offline.

## Compatibility

- **Game**: Peak
- **Framework**: BepInEx 5.4.21+
- **Platform**: Windows/Steam

## Troubleshooting

If the plugin doesn't work:
1. Make sure BepInEx is correctly installed
2. Check the BepInEx logs in `BepInEx/LogOutput.log`
3. Restart the game after installation

## Changelog

### v1.0.0

**New Features:**
- Completely redesigned UI that fits the design of the game
- Difficulty tracking implemented - Ascent level is now saved and can be used as a filter to display climbs
- Optimized climb storage with reduced memory usage locally and on the server
- Route display no longer blocks the view in the game

**Fixes & Optimizations:**
- Fixed lag spikes when activating campfires
- Only 25 climbs are loaded for each filter instead of all available ones
- List updates automatically - visible climbs always appear at the top of the list
- Sorting organizes cloud and local climbs by duration

### v0.1.0
- Initial release
- Basic path recording and visualization
- Cloud synchronization
- User interface with F1

## Support

For issues or questions, create an issue in the [GitHub Repository](https://github.com/ThomasAusHH/FollowMe-Peak).
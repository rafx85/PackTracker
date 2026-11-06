# PackTracker [![Github All Downloads](https://img.shields.io/github/downloads/rafx85/PackTracker/total.svg)](https://github.com/rafx85/PackTracker/releases)
Plugin for [HearthstoneDeckTracker](https://hsdecktracker.net/)

Pack Tacker is a small but handy plugin for Hearthstone Deck Tacker that allows you to keep an eye on every pack you open. 
This allows you to see how many cards of different rarities have dropped over time and also enables you to estimate when your next Epic or Legendary is coming!

## Features [![GitHub Latest](https://img.shields.io/github/v/release/rafx85/PackTracker)](https://github.com/rafx85/PackTracker/releases/latest) [![GitHub Latest Downloads](https://img.shields.io/github/downloads/rafx85/PackTracker/latest/total.svg)](https://github.com/rafx85/PackTracker/releases/latest)
- Tracks your pack openings
  - **Fully Automatic. No hassle with spreadsheets**
- Data can be represented in
  - a date-based (calender-like) history
  - a boosterpack-series-based statistic
  - a plain chronological text-log
- Supports auto-update
- Counts boosterpack-series-based **Pity Timer** for Epic and Legendary cards
  - Represented as bar charts
  - Overlay while opening packs (See your pity timers counting live)
- Show the overall amount of packs you've ever got
- Can search through card names and texts in both, english and the chosen language of your HDT at once
- **All stored locally. No hassle with overloaded webpages on release days**

[Download](https://github.com/rafx85/PackTracker/releases/latest) and unzip  
[How to add plugins to HDT](https://github.com/HearthSim/Hearthstone-Deck-Tracker/wiki/Available-Plugins)

## Updates

Open **Pack Tracker → Settings → Update** to check for a new release. When a newer package is available, choose **Install** and confirm the HDT restart. PackTracker verifies the GitHub SHA-256 digest and both assembly versions before restarting. The installer backs up the installed files and restores them if installation fails.

To upgrade from the previous updater, close HDT and extract the new `PackTracker.zip` over your existing PackTracker installation once.

To publish an update, set the version in `Directory.Build.props` and push the matching `v<version>` tag. GitHub Actions builds and tests the tagged commit, verifies the installer, and attaches `PackTracker.zip` to its GitHub release. Branch builds produce the same ZIP as a CI artifact.

## Screenshots

#### Pity Timers  
![](doc/Screenshots/PityTimer.png)

#### Statistic  
![](doc/Screenshots/Statistic.png)

#### History  
![](doc/Screenshots/History.png)  

#### Log  
![](doc/Screenshots/Log.png)

#### Search  
![](doc/Screenshots/Search.png)

#### Manual Add  
![](doc/Screenshots/ManualAdd.png)

# MordorFormats
A C# library for file formats in the Shadow of Mordor and Shadow of War games.  

# Formats
Format | Extension        | Description
------ | ---------------- | -----------
LTAR   | .arch05, .arch06 | LTAR presumbly stands for Lithtech Archive; They hold many game files.  

# Building
Clone or download this project somewhere:  
```
git clone https://github.com/WarpZephyr/MordorFormats.git  
```

This project requires the following libraries to be cloned alongside it.  
Place them in the same top-level folder as this project.  
These dependencies may change at any time.  
```
git clone https://github.com/WarpZephyr/OodleCoreSharp.git  
git clone https://github.com/WarpZephyr/Edoke.git  
```

Then build the project in Visual Studio 2022.  
Other IDEs or build solutions are untested.  

# Credits
Thanks to QuickBMS's [shadow_of_war.bms](https://aluigi.altervista.org/bms/shadow_of_mordor.bms) script for helping me understand how these formats work.
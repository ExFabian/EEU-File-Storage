# EEU-File-Storage
A bot for the game Everybody Edits Universe that lets you write files into worlds and later read them back onto your device, using the game as free file storage! Sadly, it's not very useful storage, as the biggest world size of 600x400 can store only up to... ~14.4 MB. Which is not alot.

This project was inspired by [DvorakDwarf's](https://github.com/DvorakDwarf) [Infinite Storage Glitch](https://github.com/DvorakDwarf/Infinite-Storage-Glitch) which is basically the same thing but with YouTube videos
instead of EEU worlds.

Shoutout to [EEJesse](https://github.com/EEJesse) for making the [EEUniverse Library for C#](https://github.com/EEUniverse/Library) which this project is based on, and luciferx for writing the [EEU Protocol](https://web.archive.org/web/20220309162754/https://luciferx.net/eeu/protocol).

# How to use
After running the executable you will be prompted to paste in your token so the bot can login through your account. There are two types of tokens you can obtain:

### NEVER let someone else get a hold of your token, since these can be singlehandedly used to login into your EEU account, or even your GOOGLE ACCOUNT if you use the Google auth method.

## 1. EEU Token
This token is the ID of your EEU account. It expires 15 minutes after being generated, which means after that time you will need to generate a new token in order to login with the bot. (Online bots will not be disconnected after this period, though.)

To generate this token, while on the EEU page press F12 to enter Developer Tools, then go to Storage > Cookies > https://ee-universe.com. The token is the string next to where it says "token".

## 2. Google auth cookie / token
This cookie can be used to authenticate to your Google account, and it expires 1 month after being generated. It is imperative that you NEVER let someone else have access to your Google token, since if they know what they're doing they can get full access to your whole Google account!

To generate this token, press F12 to enter Developer Tools and then reload the page. Wait for the game to load, then press on the Network tab and search for "iframe". Click on the accounts.google.com option and a new window with headers will show up. Scroll down to "Request Headers". The token / cookie is the value of the "Cookie" variable.

After you copied your token, paste it in the terminal and press the ENTER key.

Next you will be prompted to enter the world ID, which can be obtained in-game by opening the menu and pressing on "Copy world ID". Paste it in and press the ENTER key. The bot should now join the provided world.

The bot is used by typing in different commands in the game chat. You can check the list of the commands by sending "!help". Here is a brief guide on how to store and read a file using this bot:
- Send "!maxsize" to see how many bytes your world can store
- Copy the desired file in the folder with the executable (optional, you can use absolute paths if you want, ex. C:/My_Folder/file.txt)
- Send "!write [file name]" (if you copied the file) or "!write [absolute path]" otherwise in the chat
- Send "!verify" to check that the file got stored correctly
- Send "!read" to download the file back onto your computer
- The file has been created in the /out folder

# Building
Simply open the EEUFileStorage.csproj file with Visual Studio and build it there, or alternatively use dotnet build:
```
dotnet build ProjectFile.csproj
```
As a sidenote, if you want to look through my code or even contribute to the project, I am a beginner programmer so the code I made might be horrible. It would be appreciate it though if you have some advice for me on how I can improve my code :)

# How it works
The mechanism is very simple, literally just store the bytes of the file in the world using signs. Since the max number of characters in a sign is 120, we can store up to 60 bytes in one sign. It would be possible to fit more bytes in a block by using higher number bases than hex, or using different sign orientations, colors and bg blocks to represent different bits, however I didn't feel like going through that hassle. I might revisit this in the future, though. 

With the current method, in the biggest world ever offered by this game (600x400), you can store 600 * 400 * 60 bytes (in the real deal we're subtracting 60 because one sign is used for storing the file name and the MD5 hash of the file), which amounts to the grant total of 14,400,000 bytes, or 14.4 megabytes... Not much at all, but you can at least store a couple of cat pictures in a zip file, or 5 copies of DOOM (1993).

Other common world sizes include:
- 50x50 - 150000 bytes (150KB)
- 100x100 - 600000 bytes (600KB)
- 200x200 - 2400000 bytes (2.4MB)

using EEUniverse.Library;
using EEUniverse.LoginExtensions;
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace eeuthingy
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            Client client = new Client("");
            var token = "";
            ConsoleKey key;
            bool mask = true;
            bool valid = true;
            bool edit = false;

            do
            {
                token = "";
                valid = true;
                Console.Clear();
                Console.WriteLine("Paste token here (press F2 to toggle masking):");
                do
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    key = keyInfo.Key;

                    if (key == ConsoleKey.Backspace && token.Length > 0)
                    {
                        Console.Write("\b \b");
                        token = token[0..^1];
                    }
                    else if (!char.IsControl(keyInfo.KeyChar))
                    {
                        if (mask)
                            Console.Write("*");
                        else
                            Console.Write(key);
                        token += keyInfo.KeyChar;
                    }
                    if (key == ConsoleKey.F2)
                    {
                        mask = !mask;
                        Console.SetCursorPosition(0, 1); //set the cursor position to the start
                        for (var i = 0; i < token.Length; i++)
                        {
                            if (mask)
                                Console.Write("*");
                            else
                                Console.Write(token[i]);
                        }
                    }
                } while (key != ConsoleKey.Enter);

                Console.WriteLine("\nPlease wait...");

                try
                {
                    if (token.StartsWith("__Host"))
                        client = await GoogleLogin.GetClientFromCookieAsync(token);
                    else
                        client = new Client(token);
                    await client.ConnectAsync();
                }
                catch (Exception e)
                {
                    valid = false;
                    Console.Clear();
                    Console.WriteLine($"Couldn't connect to the EEU server, check if the token was entered correctly.\nException: {e}\nPress the ENTER key to try again.");
                    Console.ReadLine();
                }
            } while (!valid);

            Console.Clear();
            Console.WriteLine("Enter world ID:");
            var worldId = Console.ReadLine();
            var connection = client.CreateWorldConnection(worldId);
            bool connected = false;

            var botId = -1; var worldWidth = 0; var worldHeight = 0;
            const int maxBytes = 60; //max characters in a sign in 120, bytes can be represented by up to 2 characters so we have a max of 60 bytes per sign
            const int maxNameLength = 85;
            const string outPath = "out/";
            Block[,] World = {};

            Console.WriteLine("Waiting for init...");

            Task.Delay(5000).ContinueWith(t => 
            {
                if (!connected)
                    Console.WriteLine("5 seconds have passed without connecting, very likely something has gone wrong. Maybe the world ID is incorrect, the world is private or the servers are down. Restart this program and try again.");
            });

            connection.OnMessage += async (s, m) =>
            {
                if (m.Type == MessageType.Init)
                {
                    connected = true;
                    Console.Clear();
                    Console.WriteLine($"Connected to world {worldId}!");
                    botId = m.GetInt(0);
                    worldWidth = m.GetInt(9);
                    worldHeight = m.GetInt(10);
                    if (m.GetString(1) == m.GetString(7)) //if bot name is the same as owner name that means we have edit rights
                        edit = true;
                    World = new Block[worldWidth, worldHeight];
                    var index = 0;
                    for (var y = 0; y < worldHeight; y++)
                        for (var x = 0; x < worldWidth; x++)
                        {
                            var bId = m.GetInt(11 + index);
                            bId &= 0xFFFF; //gets the foreground id, we don't care about backgrounds
                            if (bId >= 55 && bId <= 58)
                            {
                                World[x, y] = new Block(bId, m.GetString(11 + index + 1));
                                index += 3;
                            }
                            else
                            {
                                World[x, y] = new Block(bId);
                                if (bId == 59) //portal
                                    index += 5;
                                else if (bId == 100 || bId == 103 || bId == 104 || bId == 105) //switch doors, coin doors
                                    index += 3;
                                else if (bId == 93 || bId == 94 || bId == 98 || bId == 99 || bId == 101 || bId == 102) //switches, effects
                                    index += 2;
                                else
                                    index++;
                            }
                        }
                    Chat("Connected!");
                }
                else if (m.Type == MessageType.PlaceBlock)
                {
                    if (m.GetInt(1) == 1) //only recieve foreground blocks
                    {
                        var bId = m.GetInt(4);
                        World[m.GetInt(2), m.GetInt(3)].id = bId;
                        if (bId >= 55 && bId <= 58)
                            World[m.GetInt(2), m.GetInt(3)].text = m.GetString(5);
                    }
                }
                else if (m.Type == MessageType.Clear)
                {
                    for (var y = 0; y < worldHeight; y++) //clears the world
                        for (var x = 0; x < worldWidth; x++)
                        {
                            World[x, y].id = 0;
                            World[x, y].text = "";
                        }
                }
                else if(m.Type == MessageType.CanEdit)
                {
                    if (m.GetInt(0) == botId)
                        edit = m.GetBool(1);
                }
                else if (m.Type == MessageType.Chat)
                {
                    if (m.GetInt(0) != botId)
                    {
                        var chatMsg = m.GetString(1);
                        if (chatMsg.StartsWith("!help ") || chatMsg == ("!help"))
                        {
                            printHelp(chatMsg);
                        }
                        else if (chatMsg.StartsWith("!write ") || chatMsg == ("!write"))
                        {
                            try
                            {
                                await writeFile(chatMsg);
                            }
                            catch (Exception e)
                            {
                                Chat($"write: An error has occured. Check console for details.");
                                Console.WriteLine($"An error has occured.\nException: {e}");
                            }
                        }
                        else if (chatMsg.StartsWith("!read ") || chatMsg == ("!read"))
                        {
                            try
                            {
                                readFile(chatMsg);
                            }
                            catch (Exception e)
                            {
                                Chat($"read: An error has occured. Check console for details.");
                                Console.WriteLine($"An error has occured.\nException: {e}");
                            }
                        }
                        else if (chatMsg.StartsWith("!verify ") || chatMsg == ("!verify"))
                        {
                            try
                            {
                                verifyFile();
                            }
                            catch (Exception e)
                            {
                                Chat($"verify: An error has occured. Check console for details.");
                                Console.WriteLine($"An error has occured.\nException: {e}");
                            }
                        }
                        else if (chatMsg.StartsWith("!maxsize ") || chatMsg == ("!maxsize"))
                        {
                            printMaxSize();
                        }
                    }
                }
            };
            await connection.SendAsync(MessageType.Init, 0);

            Thread.Sleep(-1);

            async void Chat(string msg)
            {
                await connection.SendAsync(MessageType.Chat, msg);
            }

            //command methods

            void printHelp(string chatMsg)
            {
                string[] param = chatMsg.Split();
                if (param.Length <= 1)
                {
                    Chat("Command list, type \"!help [command]\" for more details: !help !write !read !verify !maxsize");
                    return;
                }

                if (param[1][0] == '!')
                    param[1] = param[1].Substring(1);
                switch (param[1])
                {
                    case "help":
                        Chat("!help - Sends a list of the commands available.");
                        Chat("!help [command] - Sends the detailed explanation and usage of a command.");
                        break;

                    case "write":
                        Chat("!write [path to file] - Clears the world and writes the file provided to the world.");
                        break;

                    case "read":
                        Chat("!read - Reads the contents of the world into a file and creates it in the folder /out. Any files with the same name will be overwritten.");
                        Chat("!read -i [new file name] - Ignores the blue info sign at (0, 0) when reading. This requires choosing a new file name and skipping hash checking.");
                        break;

                    case "verify":
                        Chat("!verify - Checks the file stored in the world for any errors. If no errors are detected, sends some stats about the file.");
                        break;

                    case "maxsize":
                        Chat("!maxsize - Get the max file size in bytes that this world can store.");
                        break;

                    default:
                        Chat($"Command \"{param[1]}\" doesn't exist.");
                        break;
                }
            }

            async Task writeFile(string chatMsg)
            {
                if(!edit)
                {
                    Chat($"write: I don't have edit rights.");
                    return;
                }

                string[] param = chatMsg.Split();

                if (param.Length <= 1)
                {
                    Chat($"write: You must provide the path to the file.");
                    return;
                }

                var filePath = chatMsg.Substring(chatMsg.IndexOf(' ') + 1);
                if (!File.Exists(filePath))
                {
                    Chat($"write: File \"{filePath}\" does not exist.");
                    return;
                }

                var fileName = Path.GetFileName(filePath);
                if (fileName.Length > maxNameLength)
                {
                    Chat($"write: File name is longer than the max of {maxNameLength} characters. Shorten it and try again.");
                    return;
                }

                using (var fs = new FileStream(filePath, FileMode.Open))
                {
                    var fileLen = (int)fs.Length;
                    if (fileLen > worldWidth * worldHeight * maxBytes - maxBytes)
                    {
                        if (fileName.Length > 50)
                        {
                            fileName = fileName.Substring(0, 50);
                            fileName += "...";
                        }
                        Chat($"write: File \"{filePath}\" is too big. Max bytes: {worldWidth * worldHeight * maxBytes}, file size: {fileLen} bytes");
                        return;
                    }

                    if (fileLen == 0)
                    {
                        Chat($"write: File \"{filePath}\" is empty.");
                        return;
                    }

                    for (var y = 0; y < worldHeight; y++) //clears the world
                        for (var x = 0; x < worldWidth; x++)
                        {
                            World[x, y] = new Block(0);
                            await connection.SendAsync(MessageType.PlaceBlock, 1, x, y, 0);
                        }

                    var bytes = new byte[fileLen];
                    fs.Read(bytes, 0, fileLen);
                    for (int i = 0; i < fileLen; i += 16)
                    {
                        var cnt = Math.Min(16, fileLen - i);
                        var line = new byte[cnt];
                        Array.Copy(bytes, i, line, 0, cnt);
                    }

                    var byteIndex = 0;
                    var signX = 1; var signY = 0;
                    while (byteIndex < bytes.Length)
                    {
                        var i = 0;
                        var byteString = "";
                        while (i < bytes.Length - byteIndex && i < maxBytes)
                        {
                            byteString += $"{bytes[byteIndex + i]:X2}";
                            i++;
                        }
                        await connection.SendAsync(MessageType.PlaceBlock, 1, signX, signY, 55, byteString);
                        World[signX, signY] = new Block(55, byteString);
                        byteIndex += i;
                        signX++;
                        if (signX >= worldWidth)
                        {
                            signX = 0;
                            signY++;
                        }
                    }

                    string MD5Hash;
                    using (MD5 md5 = MD5.Create())
                        MD5Hash = Convert.ToHexString(md5.ComputeHash(bytes)); //calculate md5 hash and put it in a hex string
                    await connection.SendAsync(MessageType.PlaceBlock, 1, 0, 0, 58, fileName + "|" + MD5Hash); //store the filename and md5 hash in a blue sign located at 0, 0
                    World[0, 0] = new Block(58, fileName + "|" + MD5Hash);

                    Chat($"write: All done, bytes written: {bytes.Length}");
                }
            }

            void readFile(string chatMsg)
            {
                string[] param = chatMsg.Split();

                Regex rg = new Regex("[^0-9A-F]+"); //checks if a string contains characters not used to represent hex numbers
                Regex nameRg = new Regex("[\\\\\\/:\\*\\?\\\"<>\\|]+"); //checks for characters that can't be in a file name

                bool skipInfo = false;
                if (param.Length >= 2)
                {
                    if (param[1] == "-i")
                    {
                        if (param.Length == 2)
                        {
                            Chat("read: You must provide a new name for the file.");
                            return;
                        }

                        int index = chatMsg.IndexOf(' ');
                        while (!chatMsg.Substring(index).StartsWith(param[2])) //accounting for multiple spaces between "-i" and file name (there's probably a better way to do this)
                            index = chatMsg.IndexOf(' ', index + 1) + 1;
                        param[2] = chatMsg.Substring(index); //everything after "-i" is considered the file name

                        if (param[2].Contains(@"\") || param[2].Contains("/"))
                        {
                            Chat(@"read: Reading in directories other than the default not allowed, don't use '/' or '\'.");
                            return;
                        }

                        if (param[2].Length > maxNameLength)
                        {
                            Chat($"read: File name is longer than the max of {maxNameLength} characters. Shorten it and try again.");
                            return;
                        }

                        if (nameRg.IsMatch(param[2]))
                        {
                            Chat("read: File name contains invalid characters.");
                            return;
                        }

                        skipInfo = true;
                    }
                }

                if (!skipInfo)
                {
                    if (World[1, 0].id != 55 || World[1, 0].text == "")
                    {
                        Chat($"read: No file detected in this world.");
                        return;
                    }

                    if (World[0, 0].id != 58 || World[0, 0].text == "")
                    {
                        Chat($"read: Blue info sign not found or is empty. Use !read -i [new file name] to ignore this warning.");
                        return;
                    }
                }

                string[] infoParam = World[0, 0].text.Split("|");
                if (!skipInfo)
                {
                    if (infoParam.Length > 2)
                    {
                        Chat($"read: Blue info sign is invalid. Use !read -i [new file name] to ignore this warning.");
                        return;
                    }
                    if (infoParam.Length == 1)
                    {
                        Chat($"read: MD5 hash not found. Use !read -i [new file name] to ignore this warning.");
                        return;
                    }
                    if (nameRg.IsMatch(infoParam[0]))
                    {
                        Chat($"read: File name contains invalid characters. Try removing the invalid characters in the blue sign. Otherwise, use !read -i [new file name] to ignore this warning.");
                        return;
                    }
                    if (infoParam[1].Length != 32)
                    {
                        Chat($"read: MD5 hash is invalid. Use !read -i [new file name] to ignore this warning.");
                        return;
                    }
                    if (rg.IsMatch(infoParam[1]))
                    {
                        Chat($"read: MD5 hash contains invalid characters. Use !read -i [new file name] to ignore this warning.");
                        return;
                    }
                }

                var filePath = "";
                var MD5Hash = "";
                if (!skipInfo)
                {
                    filePath = outPath + infoParam[0];
                    MD5Hash = infoParam[1];
                }
                else
                    filePath = outPath + param[2];

                if (Path.GetDirectoryName(filePath) != "") //if GetDirectoryName is null CreateDirectory will fail and throw an exception
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                using (var fs = new FileStream(filePath, FileMode.Create))
                using (var bw = new BinaryWriter(fs))
                {
                    var fileLen = (int)fs.Length;
                    var signX = 1; var signY = 0;
                    bool errors = false;
                    var byteString = ""; var prevByteString = "";
                    List<byte> bytes = new List<byte>(); //used for checking md5 hash

                    while (World[signX, signY].id == 55 && signY < worldHeight)
                    {
                        prevByteString = byteString;
                        byteString = World[signX, signY].text;

                        if (rg.IsMatch(byteString) || byteString.Length % 2 == 1)
                        {
                            Chat($"read: Invalid sign content, stopping. The file created may be corrupted. x = {signX}, y = {signY}");
                            return;
                        }

                        if (prevByteString.Length < maxBytes * 2 && (signX > 1 || signY > 0))
                        {
                            var errX = signX - 1; var errY = signY;
                            if (errX < 0)
                            {
                                errX = worldWidth - 1;
                                errY--;
                            }
                            Chat($"read: Warning: Sign containing less than {maxBytes * 2} bytes detected before end of file. The file created may be corrupted. x = {errX}, y = {errY}, length: {prevByteString.Length}");
                            errors = true;
                        }

                        byte[] curBytes = Convert.FromHexString(byteString);
                        if(!skipInfo)
                            bytes.AddRange(curBytes);
                        bw.Write(curBytes);

                        signX++;
                        if (signX >= worldWidth)
                        {
                            signX = 0;
                            signY++;
                        }
                    }

                    var readMD5Hash = "";
                    if(!skipInfo)
                        using (MD5 md5 = MD5.Create())
                            readMD5Hash = Convert.ToHexString(md5.ComputeHash(bytes.ToArray()));

                    if(!skipInfo && readMD5Hash != MD5Hash)
                    {
                        Chat($"read: MD5 hash mismatch! File has been modified or corrupted. (Stored hash: {MD5Hash}, actual hash: {readMD5Hash})");
                        errors = true;
                    }

                    if (!errors)
                        Chat($"read: The file \"{filePath}\" has been successfully created.");
                    else
                        Chat($"read: The file \"{filePath}\" has been created, but it might be corrupted. Check previous messages for details.");
                }

                return;
            }

            void verifyFile()
            {
                var success = 1;
                var infoSuccess = true;
                var signX = 1; var signY = 0;
                var fileSize = 0;
                var fileName = "";
                Regex rg = new Regex("[^0-9A-F]+");
                Regex nameRg = new Regex("[\\\\\\/:\\*\\?\\\"<>\\|]+");
                var byteString = ""; var prevByteString = "";
                var MD5Hash = "";
                List<byte> bytes = new List<byte>(); //used for checking md5 hash

                if (World[1, 0].id != 55 || World[1, 0].text == "")
                {
                    Chat("verify: No file detected in this world.");
                    return;
                }
                if (World[0, 0].id != 58 || World[0, 0].text == "")
                {
                    Chat("verify: Blue info sign not found or is empty. Use !read -i [new file name] to ignore this warning.");
                    infoSuccess = false;
                }
                else 
                { 
                    string[] infoParam = World[0, 0].text.Split("|");
                    if (infoParam.Length > 2)
                    {
                        Chat($"verify: Blue info sign is invalid. Use !read -i [new file name] to ignore this warning.");
                        infoSuccess = false;
                    }
                    else if (infoParam.Length == 1)
                    {
                        Chat($"verify: MD5 hash not found. Use !read -i [new file name] to ignore this warning.");
                        infoSuccess = false;
                    }
                    else if (nameRg.IsMatch(infoParam[0]))
                    {
                        Chat($"verify: File name contains invalid characters. Use !read -i [new file name] to ignore this warning.");
                        infoSuccess = false;
                    }
                    if (infoParam[1].Length != 32)
                    {
                        Chat($"verify: MD5 hash is invalid. Use !read -i [new file name] to ignore this warning.");
                        infoSuccess = false;
                    }
                    else if (rg.IsMatch(infoParam[1]))
                    {
                        Chat($"verify: MD5 hash contains invalid characters. Use !read -i [new file name] to ignore this warning.");
                        infoSuccess = false;
                    }

                    if (infoSuccess)
                    {
                        MD5Hash = infoParam[1];
                        fileName = infoParam[0];
                    }
                }

                while (World[signX, signY].id == 55 && signY < worldHeight)
                {
                    prevByteString = byteString;
                    byteString = World[signX, signY].text;
                    if (rg.IsMatch(byteString))
                    {
                        Chat($"verify: Detected sign that contains invalid characters, reading will fail. x = {signX}, y = {signY}");
                        success = 0;
                    }
                    if (byteString.Length % 2 == 1)
                    {
                        Chat($"verify: Detected sign that contains odd number of bits, reading will fail. x = {signX}, y = {signY}");
                        success = 0;
                    }
                    if (byteString.Length == 0)
                    {
                        Chat($"verify: Detected empty sign. Reading may result in a corrupted file. x = {signX}, y = {signY}");
                        if (success > 0)
                            success = 2;
                    }
                    if (prevByteString.Length < maxBytes * 2 && (signX > 1 || signY > 0))
                    {
                        var errX = signX - 1; var errY = signY;
                        if (errX < 0)
                        {
                            errX = worldWidth - 1;
                            errY--;
                        }
                        Chat($"verify: Sign containing less than {maxBytes * 2} bytes detected before end of file. Reading may result in a corrupted file. x = {errX}, y = {errY}, length: {prevByteString.Length}");
                        if (success > 0)
                            success = 2;
                    }

                    bytes.AddRange(Convert.FromHexString(byteString));
                    fileSize += byteString.Length / 2;

                    signX++;
                    if (signX >= worldWidth)
                    {
                        signX = 0;
                        signY++;
                    }
                }

                var readMD5Hash = "";
                using (MD5 md5 = MD5.Create())
                    readMD5Hash = Convert.ToHexString(md5.ComputeHash(bytes.ToArray()));

                if (success > 0 && infoSuccess)
                {
                    if (MD5Hash != readMD5Hash)
                    {
                        Chat($"verify: MD5 hash mismatch! File has been modified or corrupted. (Stored hash: {MD5Hash}, actual hash: {readMD5Hash})");
                        MD5Hash = readMD5Hash;
                        success = 2;
                    }
                }

                if (!infoSuccess)
                    MD5Hash = readMD5Hash;

                if (fileName.Length > 50)
                {
                    fileName = fileName.Substring(0, 50);
                    fileName += "...";
                }
                if (success == 1)
                {
                    var msg = $"verify: File contains no errors.";
                    if (infoSuccess)
                    {
                        msg += $" File name: \"{fileName}\", file size: {fileSize} bytes";
                        if (fileSize >= 1_000_000)
                            msg += $" (~{fileSize / 1_000_000.0:0.00} MB)";
                        else if (fileSize >= 1_000)
                            msg += $" (~{fileSize / 1_000.0:0.00} KB)";
                        msg += ".";
                    }
                    Chat(msg);
                    Chat($"verify: MD5 hash: {MD5Hash}");
                }
                else if (success == 2)
                {
                    var msg = "verify: File contains errors, but can be read. The file may be corrupted upon creation.";
                    if (infoSuccess)
                    {
                        msg += $" File name: \"{fileName}\", file size: {fileSize} bytes";
                        if (fileSize >= 1_000_000)
                            msg += $" (~{fileSize / 1_000_000.0:0.00} MB)";
                        else if (fileSize >= 1_000)
                            msg += $" (~{fileSize / 1_000.0:0.00} KB)";
                        msg += ".";
                    }
                    Chat(msg);
                    Chat($"verify: MD5 hash: {MD5Hash}");
                }
                else
                    Chat($"verify: File contains errors and cannot be read.");

                if(!infoSuccess && success > 0)
                    Chat($"verify: Blue info sign is invalid and cannot be read. You can skip reading it by using !read -i [new file name]. Note that this also skips hash checking.");
            }

            void printMaxSize()
            {
                var maxSize = worldWidth * worldHeight * maxBytes - maxBytes;
                var msg = $"maxsize: This world can fit {maxSize} bytes";
                if (maxSize >= 1_000_000)
                    msg += $" (~{maxSize / 1_000_000.0:0.00} MB)";
                else if (maxSize >= 1_000)
                    msg += $" (~{maxSize / 1_000.0:0.00} KB)";
                msg += ".";
                Chat(msg);
            }
        }
    }

    public class Block
    {
        public int id;
        public string text;
        public Block(int id, string text = "")
        {
            this.id = id;
            this.text = text;
        }
    }
}

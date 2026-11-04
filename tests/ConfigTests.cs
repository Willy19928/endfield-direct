// SPDX-License-Identifier: GPL-3.0-only
// Copyright (c) 2026 Willy19928
using System;
using System.IO;
using System.Web.Script.Serialization;

static class ConfigTests {
    static int checks;
    static void Check(bool value, string name) {
        if (!value) throw new Exception(name);
        checks++;
    }
    static void Reject(string root, string json, string name) {
        File.WriteAllText(Path.Combine(root, "config.json"), json);
        try { AppConfig.Load(root); }
        catch (Exception error) {
            if (error is InvalidDataException || error is ArgumentException || error is IOException) { checks++; return; }
            throw;
        }
        throw new Exception("未攔下錯誤設定：" + name);
    }
    static int Main() {
        string root = Path.Combine(Path.GetTempPath(), "EndfieldDirect-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string original = Environment.CurrentDirectory;
        try {
            try { AppConfig.Load(root); throw new Exception("未攔下缺少設定檔的情況"); }
            catch (FileNotFoundException) { checks++; }
            string folder = Path.Combine(root, "遊戲 folder"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "Endfield.exe"), "測試用假檔案，不會執行");
            File.WriteAllText(Path.Combine(folder, "Games.exe"), "測試用假檔案，不會執行");
            var serializer = new JavaScriptSerializer();
            File.WriteAllText(Path.Combine(root, "config.json"), serializer.Serialize(new {
                gamePath = @"遊戲 folder\Endfield.exe", launcherPath = @"遊戲 folder\Games.exe"
            }));
            Environment.CurrentDirectory = Path.GetPathRoot(root);
            AppConfig config = AppConfig.Load(root);
            Check(config.GamePath == Path.Combine(folder, "Endfield.exe"), "含中文的相對路徑不受工作目錄影響");
            Check(config.LauncherPath == Path.Combine(folder, "Games.exe"), "指定啟動器路徑");
            File.WriteAllText(Path.Combine(root, "config.json"), serializer.Serialize(new {
                gamePath = Path.Combine(folder, "Endfield.exe"), launcherPath = ""
            }));
            Check(AppConfig.Load(root).LauncherPath == null, "啟動器路徑留空時自動尋找");
            File.WriteAllText(Path.Combine(root, "config.json"), serializer.Serialize(new { gamePath = Path.Combine(folder, "Endfield.exe") }));
            Check(AppConfig.Load(root).LauncherPath == null, "未填啟動器路徑時自動尋找");
            Reject(root, "{", "JSON 格式錯誤");
            Reject(root, "null", "設定內容為 null");
            Reject(root, "[]", "設定內容為陣列");
            Reject(root, "{}", "缺少遊戲路徑");
            Reject(root, "{\"gamePath\":42}", "遊戲路徑不是字串");
            Reject(root, "{\"gamePath\":\" \"}", "遊戲路徑為空白");
            Reject(root, "{\"gamePath\":\"missing/Endfield.exe\"}", "找不到執行檔");
            Reject(root, "{\"gamePath\":\"Games.exe\"}", "執行檔名稱錯誤");
            Reject(root, "{\"gamepath\":\"Endfield.exe\"}", "欄位名稱拼錯");
            Reject(root, "{\"gamePath\":\"C:Endfield.exe\"}", "磁碟相對路徑");
            Reject(root, "{\"gamePath\":\"/Endfield.exe\"}", "根相對路徑");
            Reject(root, "{\"gamePath\":\"Endfield.exe\",\"launcherPath\":null}", "啟動器路徑為 null");
            Console.WriteLine("通過：" + checks + " 項設定測試，未啟動遊戲。");
            return 0;
        } finally {
            Environment.CurrentDirectory = original;
            // 只刪除本次測試建立的暫存資料夾。
            Directory.Delete(root, true);
        }
    }
}

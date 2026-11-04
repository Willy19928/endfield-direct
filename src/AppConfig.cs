// SPDX-License-Identifier: GPL-3.0-only
// Copyright (c) 2026 Willy19928
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

sealed class AppConfig {
    public string GamePath { get; private set; }
    public string LauncherPath { get; private set; }

    public static AppConfig Load(string baseDirectory) {
        string file = Path.Combine(baseDirectory, "config.json");
        if (!File.Exists(file)) throw new FileNotFoundException(
            "找不到 config.json。請將 EXE 旁的 config.example.json 複製為 config.json，並填寫遊戲路徑。", file);
        Dictionary<string, object> values;
        try {
            var parser = new JavaScriptSerializer(); parser.MaxJsonLength = 65536;
            values = parser.DeserializeObject(File.ReadAllText(file)) as Dictionary<string, object>;
        } catch (ArgumentException error) {
            throw new InvalidDataException("config.json 格式錯誤：" + error.Message, error);
        } catch (InvalidOperationException error) {
            throw new InvalidDataException("config.json 格式錯誤：" + error.Message, error);
        }
        if (values == null) throw new InvalidDataException("config.json 必須是 JSON 物件。");
        foreach (string key in values.Keys)
            if (key != "gamePath" && key != "launcherPath")
                throw new InvalidDataException("config.json 不支援的欄位：" + key);
        string game = ReadString(values, "gamePath", true);
        string launcher = ReadString(values, "launcherPath", false);
        return new AppConfig {
            GamePath = Resolve(baseDirectory, game, "Endfield.exe", "gamePath"),
            LauncherPath = String.IsNullOrWhiteSpace(launcher) ? null : Resolve(baseDirectory, launcher, "Games.exe", "launcherPath")
        };
    }

    static string ReadString(Dictionary<string, object> values, string key, bool required) {
        object value;
        if (!values.TryGetValue(key, out value)) {
            if (required) throw new InvalidDataException("config.json 缺少 " + key + "。");
            return null;
        }
        string text = value as string;
        if (text == null || (required && String.IsNullOrWhiteSpace(text)))
            throw new InvalidDataException(key + " 必須是" + (required ? "非空白" : "") + "字串。");
        return text;
    }

    static string Resolve(string root, string value, string expectedName, string field) {
        // 不接受會隨工作目錄改變位置的磁碟相對路徑或根相對路徑。
        if (Path.IsPathRooted(value) && !value.StartsWith(@"\\") &&
            !(value.Length >= 3 && value[1] == ':' && (value[2] == '\\' || value[2] == '/')))
            throw new InvalidDataException(field + " 請使用完整路徑，或相對於 EXE 的路徑。");
        string path = Path.GetFullPath(Path.Combine(root, value));
        if (!String.Equals(Path.GetFileName(path), expectedName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(field + " 必須指向 " + expectedName + "。");
        if (!File.Exists(path)) throw new FileNotFoundException(field + " 找不到檔案：" + path, path);
        return path;
    }
}

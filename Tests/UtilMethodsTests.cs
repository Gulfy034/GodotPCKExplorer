﻿using System.Reflection;
using System.Collections;
using System.IO.Compression;
using GodotPCKExplorer;

namespace Tests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    [TestFixtureSource(typeof(MyFixtureData), nameof(MyFixtureData.FixtureParams))]
    public class UtilMethodsTests
    {
        static string ExecutableExtension
        {
            get => OperatingSystem.IsWindows() ? ".exe" : "";
        }

        internal static int ExecutableRunDelay
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    return 1000;
                }
                else
                {
                    return 1500;
                }
            }
        }

        static int GodotVersion = 0;

        static string ZipFilePath
        {
            get => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", $"../../../Test{GodotVersion}.zip");
        }

        static string PlatformFolder
        {
            get
            {
                if (OperatingSystem.IsWindows())
                    return "win";
                else if (OperatingSystem.IsLinux())
                    return "linux";
                else if (OperatingSystem.IsMacOS())
                    return "mac";
                return "";
            }
        }

        static readonly string binaries_base = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "TestBinaries");
        static readonly string binaries = Path.Combine(binaries_base, PlatformFolder);
        static readonly string pck_error = "Error: Couldn't load project data at path \".\". Is the .pck file missing?";

        readonly List<string> OriginalTestFiles = new();

        public UtilMethodsTests(int version)
        {
            GodotVersion = version;
        }

        static void Title(string name)
        {
            Console.WriteLine($"===================={name}====================");
        }

        static string Exe(string name)
        {
            return name + ExecutableExtension;
        }

        static string RemoveTimestampFromLogs(string logs)
        {
            logs = logs.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            var lines = logs.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var split = lines[i].Split(new char[] { '\t' }, 2);
                if (split.Length == 2)
                    lines[i] = split[1];
            }

            return string.Join(Environment.NewLine, lines);
        }

        static PCKVersion GetPCKVersion(string pack)
        {
            Console.WriteLine($"Getting version");
            string console_output = "";
            var ver = new PCKVersion();
            using (var output = new ConsoleOutputRedirect())
            {
                Assert.That(PCKActions.PrintInfo(pack), Is.True);
                console_output = RemoveTimestampFromLogs(output.GetOuput());
                var lines = console_output.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                foreach (var l in lines)
                {
                    if (l.StartsWith("Version string for this program: "))
                    {
                        var parts = l.Split(':');
                        ver = new PCKVersion(parts[^1]);
                    }
                }
            }

            Console.WriteLine(console_output);
            Console.WriteLine($"Got version: {ver}");
            return ver;
        }

        static void AssertHasError(string output)
        {
            Assert.That(output.Trim(), Does.StartWith(pck_error));
        }

        static void AssertNotError(string output)
        {
            Assert.That(output.Trim(), Does.Not.StartWith(pck_error));
        }

        [SetUp]
        public void GodotPCKInit()
        {
            PCKActions.Init();

            if (!Directory.Exists(binaries_base))
                Directory.CreateDirectory(binaries_base);

            ClearBinaries();

            var zip = ZipFile.OpenRead(ZipFilePath);
            foreach (var f in zip.Entries)
            {
                try
                {
                    var file = Path.Combine(binaries_base, f.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(file) ?? "");

                    if (f.FullName.StartsWith(PlatformFolder))
                        f.ExtractToFile(file, false);
                }
                catch { }
            }
        }

        [TearDown]
        public void GodotPCKClear()
        {
            PCKActions.Cleanup();
            ClearBinaries();
        }

        private void ClearBinaries()
        {
            foreach (var d in Directory.GetDirectories(binaries_base))
                Directory.Delete(d, true);
            foreach (var f in Directory.GetFiles(binaries_base, "*", SearchOption.AllDirectories))
                if (!OriginalTestFiles.Contains(f))
                    File.Delete(f);
        }

        /* TODO add UI tests?..
        [Test]
        public void TestOpenCommand()
        {
            Application.Idle += (sender, e) => Application.Exit();
            Title("Open");
            Assert.IsTrue(PCKActions.OpenPCKRun(Path.Combine(binaries, "Test.pck")));
            PCKActions.ClosePCK();

            Title("Wrong Path");
            Assert.IsFalse(PCKActions.OpenPCKRun(Path.Combine(binaries, "WrongPath/Test.pck")));
            PCKActions.ClosePCK();
        }
        */

        [Test]
        public void TestInfoCommand()
        {
            Title("Info PCK");
            Assert.That(PCKActions.PrintInfo(Path.Combine(binaries, "Test.pck")), Is.True);
            Title("Info EXE");
            Assert.That(PCKActions.PrintInfo(Path.Combine(binaries, Exe("TestEmbedded")), true), Is.True);
            Title("Wrong path");
            Assert.That(PCKActions.PrintInfo(Path.Combine(binaries, "WrongPath/Test.pck")), Is.False);
        }

        [Test]
        public void TestExtractScanPackCommand()
        {
            string exportTestPath = Path.Combine(binaries, "ExportTest");
            string testEXE = Path.Combine(binaries, Exe("Test"));
            string testPCK = Path.Combine(binaries, "Test.pck");
            string testEmbedPack = Path.Combine(binaries, Exe("TestPack"));
            string selectedFilesPck = Path.Combine(binaries, "SelecetedFiles.pck");
            string exportTestSelectedPath = Path.Combine(binaries, "ExportTestSelected");
            string newPckPath = Path.Combine(binaries, "out.pck");
            string exportTestSelectedWrongPath = Path.Combine(binaries, "ExportTestSelectedWrong");
            string overwritePath = exportTestPath + "Overwrite";
            string out_exe = Path.ChangeExtension(newPckPath, ExecutableExtension);

            Title("Extract");
            Assert.That(PCKActions.Extract(testPCK, exportTestPath, true), Is.True);

            Title("Extract Wrong Path");
            Assert.That(PCKActions.Extract(Path.Combine(binaries, "WrongPath/Test.pck"), exportTestPath, true), Is.False);

            Title("Compare content with folder");
            var list_of_files = PCKUtils.GetListOfFilesToPack(Path.GetFullPath(exportTestPath));
            Assert.Multiple(() =>
            {
                using var pck = new PCKReader();

                Assert.That(pck.OpenFile(testPCK), Is.True);
                Assert.That(list_of_files, Has.Count.EqualTo(pck.Files.Count));

                foreach (var f in list_of_files)
                    Assert.That(pck.Files.ContainsKey(f.Path), Is.True);

                pck.Close();
            });

            // select at least one file
            var rnd = new Random();
            var first = false;
            var seleceted_files = list_of_files.Where((f) =>
            {
                if (!first)
                {
                    first = true;
                    return true;
                }
                return rnd.NextDouble() > 0.5;
            }).ToList();

            Title("Extract only seleceted files and compare");
            var export_files = seleceted_files.Select((s) => s.Path);
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Extract(testPCK, exportTestSelectedPath, true, export_files), Is.True);

                var exportedSelectedList = PCKUtils.GetListOfFilesToPack(exportTestSelectedPath);
                Assert.That(seleceted_files, Has.Count.EqualTo(exportedSelectedList.Count));

                foreach (var f in export_files)
                    Assert.That(exportedSelectedList.FindIndex((l) => l.Path == f), Is.Not.EqualTo(-1));
            });

            Title("Extract only seleceted wrong files and compare");
            Assert.Multiple(() =>
            {
                var wrong_selected = export_files.ToList();
                for (int i = 0; i < wrong_selected.Count; i++)
                    wrong_selected[i] = wrong_selected[i] + "WrongFile";

                Assert.That(PCKActions.Extract(testPCK, exportTestSelectedWrongPath, true, wrong_selected), Is.True);
                Assert.That(Directory.Exists(exportTestSelectedWrongPath), Is.False);
            });

            Title("Extract empty list");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Extract(testPCK, exportTestSelectedWrongPath, true, Array.Empty<string>()), Is.False);
            });

            Title("Extract without overwrite");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Extract(testPCK, overwritePath, true), Is.True);
                var files = Directory.GetFiles(overwritePath);
                File.Delete(files[0]);
                File.WriteAllText(files[0], "Test");
                File.Delete(files[1]);

                Assert.That(PCKActions.Extract(testPCK, overwritePath, false), Is.True);

                Assert.That(File.ReadAllText(files[0]), Is.EqualTo("Test"));
                Assert.That(File.Exists(files[1]), Is.True);
            });

            Title("Pack new PCK");
            string ver = GetPCKVersion(testPCK).ToString();
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Pack(exportTestPath, newPckPath, ver), Is.True);

                if (/*!Utils.IsRunningOnMono()*/true)
                {
                    Title("Locked file");
                    string locked_file = Path.Combine(exportTestPath, "out.lock");
                    using var f = new LockedFile(locked_file);
                    Assert.That(PCKActions.Pack(exportTestPath, locked_file, ver), Is.False);
                }
            });

            Title("Wrong version and directory");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Pack(exportTestPath, newPckPath, "1234"), Is.False);
                Assert.That(PCKActions.Pack(exportTestPath, newPckPath, "123.33.2.1"), Is.False);
                Assert.That(PCKActions.Pack(exportTestPath, newPckPath, "-1.0.2.1"), Is.False);
                Assert.That(PCKActions.Pack(exportTestPath + "WrongPath", newPckPath, ver), Is.False);
            });

            // Compare new PCK content with alredy existing trusted list of files 'list_of_files'
            Title("Compare files to original list");
            Assert.Multiple(() =>
            {
                using var pck = new PCKReader();
                Assert.That(pck.OpenFile(testPCK), Is.True);
                foreach (var f in pck.Files.Keys)
                    Assert.That(list_of_files.FindIndex((l) => l.Path == f), Is.Not.EqualTo(-1));

                pck.Close();
            });

            Title("Pack embedded");
            Assert.Multiple(() =>
            {
                File.Copy(testEXE, testEmbedPack);
                Assert.That(PCKActions.Pack(exportTestPath, testEmbedPack, ver, embed: true), Is.True);
                Assert.That(File.Exists(Path.ChangeExtension(testEmbedPack, Exe(".old"))), Is.True);
            });

            Title("Pack embedded again");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Pack(exportTestPath, testEmbedPack, ver, embed: true), Is.False);
                Assert.That(File.Exists(Path.ChangeExtension(testEmbedPack, Exe(".old"))), Is.False);
            });

            Title("Pack only selected files");

            Assert.That(PCKActions.Pack(seleceted_files, selectedFilesPck, ver), Is.True);

            Title("Compare selected to pack content with new pck");
            Assert.Multiple(() =>
            {
                using var pck = new PCKReader();
                Assert.That(pck.OpenFile(selectedFilesPck), Is.True);
                Assert.That(seleceted_files, Has.Count.EqualTo(pck.Files.Count));

                foreach (var f in pck.Files.Keys)
                    Assert.That(seleceted_files.FindIndex((l) => l.Path == f), Is.Not.EqualTo(-1));

                pck.Close();
            });

            Title("Good run");

            File.Copy(testEXE, out_exe);

            using (var r = new RunAppWithOutput(out_exe, ""))
                AssertNotError(r.GetConsoleText());

            // test embed pack
            using (var r = new RunAppWithOutput(testEmbedPack, ""))
                AssertNotError(r.GetConsoleText());

            Title("Run without PCK");
            if (File.Exists(newPckPath))
                File.Delete(newPckPath);

            using (var r = new RunAppWithOutput(out_exe, ""))
                AssertHasError(r.GetConsoleText());
        }

        [Test]
        public void TestMergePCK()
        {
            string testEXE = Path.Combine(binaries, Exe("Test"));
            string testPCK = Path.Combine(binaries, "Test.pck");
            string newEXE = Path.Combine(binaries, Exe("TestMerge"));
            string newEXE1Byte = Path.Combine(binaries, Exe("TestMerge1Byte"));
            string newEXE_old = Path.Combine(binaries, Exe("TestMerge.old"));

            File.Copy(testEXE, newEXE);

            Title("Merge");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Merge(testPCK, newEXE), Is.True);
                Assert.That(File.Exists(newEXE_old), Is.True);
            });

            Title("Again");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Merge(testPCK, newEXE), Is.False);

                File.Delete(newEXE);
                File.Copy(testEXE, newEXE);
            });

            Title("Merge without backup");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Merge(testPCK, newEXE, true), Is.True);
                Assert.That(File.Exists(newEXE_old), Is.False);

                File.Delete(newEXE);
                File.Copy(testEXE, newEXE);
            });

            if (/*!Utils.IsRunningOnMono()*/true)
            {
                Title("Locked backup");
                // creates new (old + ExecutableExtension) 0kb
                using (var l = new LockedFile(newEXE_old, false))
                    Assert.That(PCKActions.Merge(testPCK, newEXE, true), Is.False);

                Title("Locked pck file");
                using (var l = new LockedFile(testPCK, false))
                    Assert.That(PCKActions.Merge(testPCK, newEXE), Is.False);
            }

            Title("Wrong Files");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Merge(testPCK + "Wrong", newEXE), Is.False);
                Assert.That(PCKActions.Merge(testPCK, newEXE + "Wrong", true), Is.False);
            });

            Title("Same File");
            Assert.That(PCKActions.Merge(testPCK, testPCK, true), Is.False);

            Title("-1 Byte");
            var nf = new BinaryWriter(File.OpenWrite(newEXE1Byte));
            var o = new BinaryReader(File.OpenRead(testEXE));
            nf.Write(o.ReadBytes((int)o.BaseStream.Length - 1), 0, (int)o.BaseStream.Length - 1);
            nf.Close();
            o.Close();

            // The result is good... but thats not 64bit multiple :/
            Assert.That(PCKActions.Merge(testPCK, newEXE1Byte, true), Is.True);

            Title("Bad run");
            using (var r = new RunAppWithOutput(newEXE, ""))
                AssertHasError(r.GetConsoleText());

            Title("Good runs");
            File.Delete(newEXE);
            File.Copy(testEXE, newEXE);
            Assert.That(PCKActions.Merge(testPCK, newEXE), Is.True);
            using (var r = new RunAppWithOutput(newEXE, ""))
                AssertNotError(r.GetConsoleText());

            using (var r = new RunAppWithOutput(newEXE1Byte, ""))
                if (GodotVersion == 3)
                    AssertNotError(r.GetConsoleText());
                else if (GodotVersion == 3)
                    AssertHasError(r.GetConsoleText());
        }

        [Test]
        public void TestRipPCK()
        {
            string new_exe = Path.Combine(binaries, Exe("TestRip"));
            string new_exe_old = Path.Combine(binaries, Exe("TestRip.old"));
            string new_pck = Path.Combine(binaries, "TestRip.pck");
            string locked_exe_str = Path.Combine(binaries, Exe("TestLockedRip"));

            File.Copy(Path.Combine(binaries, Exe("TestEmbedded")), new_exe);

            Title("Rip embedded");
            Assert.That(PCKActions.Rip(new_exe, new_pck), Is.True);

            Title("Rip wrong files");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Rip(Path.Combine(binaries, Exe("Test")), new_pck), Is.False);
                Assert.That(PCKActions.Rip(new_pck, new_pck), Is.False);

                if (/*!Utils.IsRunningOnMono()*/true)
                {
                    Title("Locked file");
                    string locked_file = Path.Combine(binaries, "test.lock");
                    using var f = new LockedFile(locked_file);
                    Assert.That(PCKActions.Rip(new_exe, locked_file), Is.False);
                }
            });

            Title("Rip PCK from exe");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Rip(new_exe, null, true), Is.True);
                Assert.That(File.Exists(new_exe_old), Is.False);
            });

            Title("Rip PCK from PCK");
            Assert.That(PCKActions.Rip(new_pck, null, true), Is.False);

            Title("Good run");
            using (var r = new RunAppWithOutput(new_exe, ""))
                AssertNotError(r.GetConsoleText());

            Title("Run without PCK");
            if (File.Exists(new_pck))
                File.Delete(new_pck);
            using (var r = new RunAppWithOutput(new_exe, ""))
                AssertHasError(r.GetConsoleText());

            Title("Rip locked");

            File.Copy(Path.Combine(binaries, Exe("TestEmbedded")), locked_exe_str);

            using (var locked_exe = File.OpenWrite(locked_exe_str))
                Assert.That(PCKActions.Rip(locked_exe_str), Is.False);

            Title("Rip and remove .old");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Rip(locked_exe_str, null, true), Is.True);
                Assert.That(File.Exists(Path.ChangeExtension(locked_exe_str, Exe(".old"))), Is.False);
            });
        }

        [Test]
        public void TestSplitPCK()
        {
            string exe = Path.Combine(binaries, Exe("TestSplit"));
            string pck = Path.Combine(binaries, "TestSplit.pck");
            string new_exe = Path.Combine(binaries, "SplitFolder", Exe("Split"));
            string new_pck = Path.Combine(binaries, "SplitFolder", "Split.pck");

            File.Copy(Path.Combine(binaries, Exe("TestEmbedded")), exe);

            Title("Split with custom pair name and check files");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Split(exe, new_exe), Is.True);
                Assert.That(File.Exists(new_exe), Is.True);
                Assert.That(File.Exists(new_pck), Is.True);
                Assert.That(File.Exists(Path.ChangeExtension(new_exe, Exe(".old"))), Is.False);
            });

            Title("Can't copy with same name");
            Assert.That(PCKActions.Split(exe, exe), Is.False);

            Title("Split with same name");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.Split(exe), Is.True);
                Assert.That(File.Exists(exe), Is.True);
                Assert.That(File.Exists(pck), Is.True);
                Assert.That(File.Exists(Path.ChangeExtension(new_exe, Exe(".old"))), Is.False);
            });

            Title("Already splitted");
            Assert.That(PCKActions.Split(exe), Is.False);

            Title("Good runs");
            using (var r = new RunAppWithOutput(exe, ""))
                AssertNotError(r.GetConsoleText());

            using (var r = new RunAppWithOutput(new_exe, ""))
                AssertNotError(r.GetConsoleText());

            Title("Bad runs");
            foreach (var f in new string[] { pck, new_pck })
                if (File.Exists(f))
                    File.Delete(f);

            using (var r = new RunAppWithOutput(exe, ""))
                AssertHasError(r.GetConsoleText());

            using (var r = new RunAppWithOutput(new_exe, ""))
                AssertHasError(r.GetConsoleText());

            if (/*!Utils.IsRunningOnMono()*/true)
            {
                Title("Split with locked output");
                foreach (var f in new string[] { new_exe, new_pck })
                    if (File.Exists(f))
                        File.Delete(f);
                File.Copy(Path.Combine(binaries, "Test.pck"), new_pck);

                using (var l = new LockedFile(new_pck, false))
                    Assert.That(PCKActions.Split(Path.Combine(binaries, Exe("TestEmbedded")), new_exe), Is.False);

                Assert.Multiple(() =>
                {
                    Assert.That(File.Exists(new_exe), Is.False);
                    Assert.That(File.Exists(new_pck), Is.True);
                });
            }
        }

        [Test]
        public void TestChangePCKVersion()
        {
            string exe = Path.Combine(binaries, Exe("TestVersion"));
            string pck = Path.Combine(binaries, "TestVersion.pck");
            string exeEmbedded = Path.Combine(binaries, Exe("TestVersionEmbedded"));

            File.Copy(Path.Combine(binaries, Exe("Test")), exe);
            File.Copy(Path.Combine(binaries, "Test.pck"), pck);
            File.Copy(Path.Combine(binaries, Exe("TestEmbedded")), exeEmbedded);

            var origVersion = GetPCKVersion(pck);
            var newVersion = origVersion;
            newVersion.Major += 1;
            newVersion.Minor += 1;
            newVersion.Revision += 2;

            Title("Regular pck test runs");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.ChangeVersion(pck, newVersion.ToString()), Is.True);
                Assert.That(GetPCKVersion(pck), Is.EqualTo(newVersion));
                using (var r = new RunAppWithOutput(exe, ""))
                    AssertHasError(r.GetConsoleText());

                Assert.That(PCKActions.ChangeVersion(pck, origVersion.ToString()), Is.True);
                Assert.That(GetPCKVersion(pck), Is.EqualTo(origVersion));

                using (var r = new RunAppWithOutput(exe, ""))
                    AssertNotError(r.GetConsoleText());
            });

            Title("Embedded test runs");
            Assert.Multiple(() =>
            {
                Assert.That(PCKActions.ChangeVersion(exeEmbedded, newVersion.ToString()), Is.True);
                Assert.That(GetPCKVersion(exeEmbedded), Is.EqualTo(newVersion));

                using (var r = new RunAppWithOutput(exeEmbedded, ""))
                    AssertHasError(r.GetConsoleText());

                Assert.That(PCKActions.ChangeVersion(exeEmbedded, origVersion.ToString()), Is.True);
                Assert.That(GetPCKVersion(exeEmbedded), Is.EqualTo(origVersion));

                using (var r = new RunAppWithOutput(exeEmbedded, ""))
                    AssertNotError(r.GetConsoleText());
            });
        }

        [Test]
        public void TestEncryption()
        {
            if (GodotVersion == 3)
                Assert.Inconclusive("Not applicable!");

            string enc_key = "7FDBF68B69B838194A6F1055395225BBA3F1C5689D08D71DCD620A7068F61CBA";
            string wrong_enc_key = "8FDBF68B69B838194A6F1055395225BBA3F1C5689D08D71DCD620A7068F61CBA";
            string ver = "2.4.0.2";

            string extracted = Path.Combine(binaries, "EncryptedExport");
            string exe = Path.Combine(binaries, Exe("TestEncrypted"));
            string pck = Path.Combine(binaries, "TestEncrypted.pck");
            string exe_new = Path.Combine(binaries, Exe("TestEncryptedNew"));
            string pck_new = Path.Combine(binaries, "TestEncryptedNew.pck");
            string pck_new_files = Path.Combine(binaries, "TestEncryptedNewOnlyFiles.pck");
            string pck_new_wrong_key = Path.Combine(binaries, "TestEncryptedWrongKey.pck");
            string exe_new_wrong_key = Path.Combine(binaries, Exe("TestEncryptedWrongKey"));
            string exe_new_files = Path.Combine(binaries, Exe("TestEncryptedNewOnlyFiles"));
            string exe_ripped = Path.Combine(binaries, Exe("TestEncryptedRipped"));
            string pck_ripped = Path.Combine(binaries, "TestEncryptedRipped.pck");
            string exe_embedded = Path.Combine(binaries, Exe("TestEncryptedEmbedded"));

            File.Copy(Path.Combine(binaries, Exe("Test")), exe);
            File.Copy(Path.Combine(binaries, Exe("Test")), exe_embedded);
            File.Copy(Path.Combine(binaries, Exe("Test")), exe_new);
            File.Copy(Path.Combine(binaries, Exe("Test")), exe_new_files);
            File.Copy(Path.Combine(binaries, Exe("Test")), exe_ripped);
            File.Copy(Path.Combine(binaries, Exe("Test")), exe_new_wrong_key);

            Title("PCK info");
            Assert.That(PCKActions.PrintInfo(pck, true, enc_key), Is.True);
            Title("PCK info wrong");
            Assert.That(PCKActions.PrintInfo(pck, true, wrong_enc_key), Is.False);

            Title("Merge PCK");
            Assert.That(PCKActions.Merge(pck, exe_embedded), Is.True);

            Title("Rip PCK");
            Assert.That(PCKActions.Rip(exe_embedded, pck_ripped), Is.True);

            Title("Extract PCK");
            Assert.That(PCKActions.Extract(pck, extracted, true, encKey: enc_key), Is.True);
            Title("Extract PCK wrong");
            Assert.That(PCKActions.Extract(pck, extracted + "Wrong", true, encKey: wrong_enc_key), Is.False);

            Title("Pack PCK");
            Assert.That(PCKActions.Pack(extracted, pck_new, ver, encIndex: true, encFiles: true, encKey: enc_key), Is.True);
            Title("Pack PCK only files");
            Assert.That(PCKActions.Pack(extracted, pck_new_files, ver, encIndex: false, encFiles: true, encKey: enc_key), Is.True);
            Title("Pack PCK wrong");
            Assert.That(PCKActions.Pack(extracted, pck_new_wrong_key, ver, encIndex: true, encFiles: true, encKey: wrong_enc_key), Is.True);
            Title("Pack PCK no key");
            Assert.That(PCKActions.Pack(extracted, pck_new_wrong_key, ver, encIndex: true, encFiles: true), Is.False);

            Title("Extract PCK. Encrypted only files");
            Assert.That(PCKActions.Extract(pck_new_files, extracted, true, encKey: enc_key), Is.True);
            Title("Extract PCK. Encrypted only files");
            Assert.That(PCKActions.Extract(pck_new_files, extracted, true), Is.False);

            Title("PCK good test runs");

            Assert.Multiple(() =>
            {
                using (var r = new RunAppWithOutput(exe, ""))
                    AssertNotError(r.GetConsoleText());

                using (var r = new RunAppWithOutput(exe_embedded, ""))
                    AssertNotError(r.GetConsoleText());

                using (var r = new RunAppWithOutput(exe_ripped, ""))
                    AssertNotError(r.GetConsoleText());

                using (var r = new RunAppWithOutput(exe_new, ""))
                    AssertNotError(r.GetConsoleText());

                using (var r = new RunAppWithOutput(exe_new_files, ""))
                    AssertNotError(r.GetConsoleText());
            });

            Title("PCK bad test runs");

            using (var r = new RunAppWithOutput(exe_new_wrong_key, ""))
                AssertHasError(r.GetConsoleText());
        }
    }

    public class MyFixtureData
    {
        public static IEnumerable FixtureParams
        {
            get
            {
                yield return new TestFixtureData(3);
                yield return new TestFixtureData(4);
            }
        }
    }
}

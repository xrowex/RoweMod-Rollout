using System.Diagnostics;
using System.Text;
using RoweMod.Core;

namespace RoweMod.App;

static class FlowPlaytest
{
    public static int Run()
    {
        var log = new StringBuilder();
        var fails = new List<string>();
        void Ok(string line) => log.AppendLine("OK  " + line);
        void Fail(string line)
        {
            fails.Add(line);
            log.AppendLine("FAIL  " + line);
        }

        try
        {
            ApplicationConfiguration.Initialize();
            var t = ToolPaths.Detect();
            var next = UiCopy.NextStep(t);
            log.AppendLine("NEXT  " + next.Title + " — " + next.Body);
            log.AppendLine("GAME  " + (t.GamePaks ?? "(none)"));
            log.AppendLine("RET   " + t.HasRetoc + "  NET " + t.HasDotnet + "  UE " + (t.UnrealEditor != null));
            log.AppendLine("COOK  pending=" + UiCopy.CookPending(t) + "  overlay=" + (t.OverlayUtoc != null));

            if (UiCopy.ButtonHover("meshrun", t) != null)
                Fail("hover still defines Mesh to game");
            else
                Ok("no Mesh to game hover");

            var pieces = ClothingLibrary.Scan(t.Repo);
            var clothes = pieces.Count(p => ClothingLibrary.IsClothingSlot(p.Slot) && !p.Sample);
            var skates = pieces.Count(ClothingLibrary.IsSkate);
            log.AppendLine("LIB   clothes=" + clothes + " skates=" + skates + " total=" + pieces.Count);

            var shotDir = Path.Combine(t.Repo, "dumps", "ui-playtest");
            Directory.CreateDirectory(shotDir);

            using (var main = new MainForm())
            {
                main.StartPosition = FormStartPosition.Manual;
                main.Location = new Point(-2000, -2000);
                main.Show();
                Application.DoEvents();
                var bar = string.Join(" | ", main.Toolbar);
                log.AppendLine("BAR   " + bar);
                if (main.Toolbar.Count != 4)
                    Fail("toolbar has " + main.Toolbar.Count + " buttons, expected 4");
                else
                    Ok("4 toolbar buttons");
                if (!bar.Contains("Create", StringComparison.Ordinal) || bar.Contains("Clothing", StringComparison.Ordinal) || bar.Contains("Cook", StringComparison.Ordinal))
                    Fail("toolbar should be Setup | Create | Gallery | Play, got " + bar);
                else
                    Ok("Create bar (no Clothing/Cook)");
                if (!string.Equals(main.HowTo.NextTitle, next.Title, StringComparison.Ordinal))
                    Fail("How-to next '" + main.HowTo.NextTitle + "' != '" + next.Title + "'");
                else
                    Ok("How-to next is " + next.Title);
                if (next.Action != UiCopy.NextAction.None)
                {
                    if (string.IsNullOrWhiteSpace(main.HowTo.CtaText) || main.HowTo.CtaText != next.Cta)
                        Fail("CTA '" + main.HowTo.CtaText + "' != '" + next.Cta + "'");
                    else
                        Ok("CTA is " + next.Cta);
                }
                else
                    Ok("no CTA when ready");
                if (main.HowTo.HoverShown)
                    Fail("hover card visible at idle");
                else
                    Ok("hover hidden at idle");
                Shot(main, Path.Combine(shotDir, "01-main.png"));
                main.HowTo.ShowButton("create");
                Application.DoEvents();
                if (!main.HowTo.HoverShown || main.HowTo.HoverTitle != "Create")
                    Fail("hover create did not show");
                else
                    Ok("hover create");
                Shot(main, Path.Combine(shotDir, "02-hover-create.png"));
                main.HowTo.ShowButton(null);
                if (main.HowTo.HoverShown)
                    Fail("hover did not hide on leave");
                else
                    Ok("hover hides on leave");
                main.Hide();
            }

            using (var create = new ExportWorkshopForm(t, WorkshopDomain.Clothing))
            {
                create.StartPosition = FormStartPosition.Manual;
                create.Location = new Point(-2000, -2000);
                create.Show();
                Application.DoEvents();
                if (create.DomainLabel != "Clothes" || create.TabLabel != "Paint")
                    Fail("create default '" + create.DomainLabel + "/" + create.TabLabel + "' != Clothes/Paint");
                else
                    Ok("create opens Clothes/Paint");
                var want = UiCopy.WorkshopBanner(WorkshopDomain.Clothing, WorkshopTab.Paint);
                if (create.BannerText != want)
                    Fail("clothes paint banner '" + create.BannerText + "' != '" + want + "'");
                else
                    Ok("clothes paint banner");
                Shot(create, Path.Combine(shotDir, "03-create-clothes.png"));
                create.ShowTab(WorkshopTab.Mesh);
                Application.DoEvents();
                if (create.TabLabel != "Shape")
                    Fail("shape tab label '" + create.TabLabel + "'");
                else
                    Ok("shape tab");
                Shot(create, Path.Combine(shotDir, "03-create-shape.png"));
                create.ShowDomain(WorkshopDomain.Skates);
                create.ShowTab(WorkshopTab.Paint);
                Application.DoEvents();
                if (create.DomainLabel != "Skates")
                    Fail("skates domain '" + create.DomainLabel + "'");
                else
                    Ok("skates domain");
                Shot(create, Path.Combine(shotDir, "04-create-skates.png"));
                create.ShowTab(WorkshopTab.Mesh);
                Application.DoEvents();
                Shot(create, Path.Combine(shotDir, "04-create-skates-shape.png"));
                create.Hide();
            }

            using (var gallery = new GalleryForm(t))
            {
                gallery.StartPosition = FormStartPosition.Manual;
                gallery.Location = new Point(-2000, -2000);
                gallery.Show();
                for (var i = 0; i < 20; i++)
                {
                    Application.DoEvents();
                    Thread.Sleep(100);
                }
                Shot(gallery, Path.Combine(shotDir, "05-gallery.png"));
                Ok("gallery constructed");
                gallery.Hide();
            }

            using (var neu = new NewMeshForm(WorkshopDomain.Clothing))
            using (var skateNew = new NewMeshForm(WorkshopDomain.Skates))
            {
                neu.CreateControl();
                skateNew.CreateControl();
                Ok("new mesh dialogs");
            }

            TestPaintPromote(t.Repo, Ok, Fail);

            log.AppendLine("PATCH …");
            var patch = DtPatcher.Program.PatchAllItems(t.Repo, Array.Empty<string>(), line => log.AppendLine("  " + line));
            if (patch != 0)
                Fail("PatchAllItems exit " + patch);
            else
                Ok("PatchAllItems 0");

            var gameOn = Process.GetProcessesByName("RollerSkate-Win64-Shipping").Length > 0
                || Process.GetProcessesByName("RollerSkate").Length > 0;
            if (t.GamePaks == null || !t.HasRetoc)
                log.AppendLine("PACK  skipped (no game/retoc)");
            else if (gameOn)
                log.AppendLine("PACK  skipped (game is running)");
            else
            {
                log.AppendLine("PACK  …");
                var packed = ModMerge.Pack(t.Repo, t.GamePaks, line => log.AppendLine("  " + line));
                if (packed != 0)
                    Fail("Pack exit " + packed);
                else
                    Ok("Pack overlay");
            }
        }
        catch (Exception ex)
        {
            Fail(ex.ToString());
        }

        log.AppendLine(fails.Count == 0 ? "RESULT  pass" : "RESULT  " + fails.Count + " fail(s)");
        var text = log.ToString();
        try
        {
            var dir = Path.Combine(ToolPaths.RepoRoot, "dumps");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "ui-check.txt"), text);
        }
        catch { /* ignore */ }
        Console.WriteLine(text);
        return fails.Count == 0 ? 0 : 1;
    }

    static void TestPaintPromote(string repo, Action<string> ok, Action<string> fail)
    {
        var albedo = Path.Combine(repo, "art", "textures", "oversized-jeans", "T_jeans-rowe-albedo.png");
        if (!File.Exists(albedo))
        {
            fail("no jeans albedo for paint probe");
            return;
        }

        var piece = new ClothingPiece
        {
            Id = "tex:paint-probe",
            Title = "paint probe",
            Slot = "Tops",
            Kind = ClothingKind.Texture,
            Table = "DT-upper",
            BasedOn = "paint-probe-unique",
            PaintFile = albedo,
        };
        var row = "paint-probe-unique-mod";
        var json = Path.Combine(repo, "items", "upper", row + ".json");
        var texDir = Path.Combine(repo, "art", "textures", row);
        try
        {
            var made = TextureMods.Promote(repo, piece);
            if (made == null)
            {
                fail("Promote returned null");
                return;
            }

            if (!File.Exists(json))
            {
                fail("missing probe json");
                return;
            }
            var text = File.ReadAllText(json);
            if (!text.Contains("\"normal\"", StringComparison.OrdinalIgnoreCase))
                fail("probe json has no normal");
            else
                ok("paint json has normal");

            var nrm = Path.Combine(texDir, "T_" + row + "-normal.png");
            if (!File.Exists(nrm))
            {
                fail("missing probe normal png");
                return;
            }
            using var bmp = new Bitmap(nrm);
            var c = bmp.GetPixel(0, 0);
            if (c.R != 128 || c.G != 128 || c.B != 255)
                fail("probe normal px=(" + c.R + "," + c.G + "," + c.B + ")");
            else
                ok("paint normal is flat DirectX");
        }
        finally
        {
            try { if (File.Exists(json)) File.Delete(json); } catch { /* cleanup */ }
            try { if (Directory.Exists(texDir)) Directory.Delete(texDir, true); } catch { /* cleanup */ }
        }
    }

    static void Shot(Form form, string path)
    {
        Application.DoEvents();
        using var bmp = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height));
        form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
        bmp.Save(path);
    }
}

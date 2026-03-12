using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Media;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;

namespace PicoLauncherApp
{
    public class PicoLauncher : Form
    {
        private TextBox txtSrc, txtDst, txtRoms;
        private Button btnStart, btnBrowseSrc, btnBrowseDst, btnBrowseRoms, btnClose, btnMin;
        private CheckBox chkClean, chkOverwrite, chkOpenDir;
        private Label lblStatus, lblRomsTitle, lblTitle;
        private PictureBox pbPreview;
        private Panel panelBarra, luzBarra;
        private System.Windows.Forms.Timer titleTimer;

        private string configFile = "config.txt";
        
        private Color neonBlue = Color.DeepSkyBlue;
        private Color darkBg = Color.FromArgb(25, 25, 28);
        private Color darkInput = Color.FromArgb(40, 40, 45);
        private Color btnGray = Color.FromArgb(55, 55, 60);

        private bool isUpdatingConfig = false;
        private string[] titles = { "DSPico Conversor v4.2", "Rikisoft 2026" };
        private int currentTitleIndex = 0;

        [DllImport("user32.dll", EntryPoint = "ReleaseCapture")]
        private extern static void ReleaseCapture();
        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private extern static void SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

        // MÉTODO CLAVE: Extraer recursos del EXE
        private Stream GetResourceStream(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            // Esta línea busca CUALQUIER recurso que termine con el nombre del archivo
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(str => str.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            
            return resourceName != null ? assembly.GetManifestResourceStream(resourceName) : null;
        }

        public PicoLauncher()
        {
            this.Size = new Size(460, 600);
            this.BackColor = darkBg;
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "DSPICO CONVERSOR v4.6";

            this.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(this.Handle, 0x112, 0xf012, 0); };

            InitializeControls();

            // --- CAMBIO APLICADO: Cargar icono para la barra de tareas ---
            try {
                using (var s = GetResourceStream("dspico-logo.png")) {
                    if (s != null) {
                        using (Bitmap bmp = (Bitmap)Image.FromStream(s)) {
                            this.Icon = Icon.FromHandle(bmp.GetHicon());
                        }
                    }
                }
            } catch { }
            // -------------------------------------------------------------

            isUpdatingConfig = true;
            LoadConfig();
            isUpdatingConfig = false;
            UpdateUI();
            CargarLogo();

            titleTimer = new System.Windows.Forms.Timer();
            titleTimer.Interval = 5000; 
            titleTimer.Tick += TitleTimer_Tick;
            titleTimer.Start();
            AnimateTitle(titles[currentTitleIndex]);
        }

        private void InitializeControls()
        {
            btnMin = new Button { 
                Text = "_", Size = new Size(25, 25), 
                Location = new Point(15, 10), 
                BackColor = btnGray, FlatStyle = FlatStyle.Flat, 
                ForeColor = Color.White, Font = new Font("Segoe UI", 7, FontStyle.Bold), Cursor = Cursors.Hand 
            };
            btnMin.FlatAppearance.BorderSize = 0;
            btnMin.Click += (s, e) => { this.WindowState = FormWindowState.Minimized; };
            this.Controls.Add(btnMin);

            btnClose = new Button { 
                Text = "×", Size = new Size(25, 25), 
                Location = new Point(420, 10), 
                BackColor = Color.FromArgb(100, 30, 30), FlatStyle = FlatStyle.Flat, 
                ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand 
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => { Application.Exit(); };
            this.Controls.Add(btnClose);

            lblTitle = new Label { 
                Text = "",
                Location = new Point(0, 15), 
                Size = new Size(460, 30), 
                TextAlign = ContentAlignment.MiddleCenter, 
                ForeColor = neonBlue, 
                Font = new Font("Courier New", 12, FontStyle.Bold)
            };
            this.Controls.Add(lblTitle);

            int cY = 55;
            AddLabel(" - CARPETA DE CARÁTULAS ORIGEN", cY); cY += 20;
            txtSrc = CreateTextBox(cY, 360);
            btnBrowseSrc = CreateSmallFolderButton(395, cY, "src"); cY += 45;

            AddLabel(" - CARPETA DE DESTINO (DSPICO)", cY); cY += 20;
            txtDst = CreateTextBox(cY, 360);
            btnBrowseDst = CreateSmallFolderButton(395, cY, "dst"); cY += 45;

            chkClean = CreateCheckBox("Limpiar carátulas sin ROM detectada", 30, cY);
            this.Controls.Add(chkClean); cY += 32;

            lblRomsTitle = AddLabel(" - CARPETA DE ROMS (MODO LIMPIEZA)", cY); cY += 20;
            txtRoms = CreateTextBox(cY, 360);
            btnBrowseRoms = CreateSmallFolderButton(395, cY, "roms"); cY += 45;

            chkOpenDir = CreateCheckBox("Abrir destino al finalizar", 30, cY);
            this.Controls.Add(chkOpenDir); cY += 28;

            chkOverwrite = CreateCheckBox("Forzar sobreescritura de archivos", 30, cY);
            this.Controls.Add(chkOverwrite); cY += 32;

            pbPreview = new PictureBox { Size = new Size(150, 150), Location = new Point((this.ClientSize.Width - 150) / 2, cY), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            this.Controls.Add(pbPreview); cY += 160;

            panelBarra = new Panel { Size = new Size(320, 4), Location = new Point((this.ClientSize.Width - 320) / 2, cY), BackColor = Color.FromArgb(45, 45, 50) };
            luzBarra = new Panel { Size = new Size(0, 4), Location = new Point(0, 0), BackColor = neonBlue };
            panelBarra.Controls.Add(luzBarra);
            this.Controls.Add(panelBarra); cY += 15;

            lblStatus = new Label { Text = "SISTEMA LISTO", Location = new Point(0, cY), Size = new Size(460, 20), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Silver, Font = new Font("Consolas", 8) };
            this.Controls.Add(lblStatus); cY += 25;

            btnStart = new Button { 
                Text = "INICIAR PROCESO", 
                Size = new Size(200, 40), 
                Location = new Point((this.ClientSize.Width - 200) / 2, cY), 
                BackColor = neonBlue, 
                FlatStyle = FlatStyle.Flat, 
                Font = new Font("Segoe UI", 9, FontStyle.Bold), 
                ForeColor = Color.Black, 
                Cursor = Cursors.Hand 
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += IniciarConversion;
            this.Controls.Add(btnStart);
        }

        private void TitleTimer_Tick(object sender, EventArgs e)
        {
            currentTitleIndex = (currentTitleIndex + 1) % titles.Length;
            AnimateTitle(titles[currentTitleIndex]);
        }

        private async void AnimateTitle(string targetText)
        {
            lblTitle.Text = "";
            int mid = targetText.Length / 2;
            for (int i = 0; i <= mid; i++)
            {
                int start = Math.Max(0, mid - i);
                int end = Math.Min(targetText.Length - 1, mid + i);
                string currentView = targetText.Substring(start, end - start + 1);
                int padding = (targetText.Length - currentView.Length) / 2;
                lblTitle.Text = new string(' ', padding) + currentView;
                await Task.Delay(50); 
            }
            lblTitle.Text = targetText;
        }

        private async void IniciarConversion(object sender, EventArgs e)
        {
            if (!Directory.Exists(txtSrc.Text) || !Directory.Exists(txtDst.Text)) { PlayCustomSound("Quitar.wav"); return; }
            
            string[] originFiles = Directory.GetFiles(txtSrc.Text, "*.*")
                .Where(s => s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".jpeg")).ToArray();

            List<string> toProcess = new List<string>();
            foreach(var f in originFiles) {
                if (chkOverwrite.Checked || !File.Exists(Path.Combine(txtDst.Text, Path.GetFileNameWithoutExtension(f) + ".bmp")))
                    toProcess.Add(f);
            }

            if (toProcess.Count == 0 && !chkClean.Checked) { lblStatus.Text = "NADA QUE PROCESAR"; PlayCustomSound("Quitar.wav"); return; }

            btnStart.Enabled = false;
            btnStart.BackColor = Color.DimGray;
            btnStart.ForeColor = Color.White;
            luzBarra.Width = 0;
            PlayCustomSound("Inicio.wav");

            await Task.Run(() => {
                for (int i = 0; i < toProcess.Count; i++) {
                    string f = toProcess[i];
                    string name = Path.GetFileNameWithoutExtension(f);
                    string outP = Path.Combine(txtDst.Text, name + ".bmp");

                    this.Invoke((System.Windows.Forms.MethodInvoker)delegate {
                        try {
                            if (pbPreview.Image != null) pbPreview.Image.Dispose();
                            using (var s = new FileStream(f, FileMode.Open, FileAccess.Read)) { pbPreview.Image = Image.FromStream(s); }
                            btnStart.Text = string.Format("PROCESANDO ({0})", toProcess.Count - i);
                            lblStatus.Text = string.Format("CONVIRTIENDO: {0}", name.ToUpper());
                            luzBarra.Width = (int)((float)(i + 1) / toProcess.Count * panelBarra.Width);
                        } catch { }
                    });

                    ProcessStartInfo psi = new ProcessStartInfo("magick.exe") {
                        Arguments = string.Format("\"{0}\" -resize 106x96! -background black -gravity northwest -extent 128x96 -type Palette -depth 8 -colors 256 -compress none \"BMP3:{1}\"", f, outP),
                        CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using (Process p = Process.Start(psi)) { if (p != null) p.WaitForExit(); }
                }

                if (chkClean.Checked && Directory.Exists(txtRoms.Text)) {
                    this.Invoke((System.Windows.Forms.MethodInvoker)delegate { lblStatus.Text = "LIMPIANDO ARCHIVOS..."; btnStart.Text = "LIMPIANDO..."; });
                    string[] bmps = Directory.GetFiles(txtDst.Text, "*.bmp");
                    foreach (string b in bmps) {
                        if (Directory.GetFiles(txtRoms.Text, Path.GetFileNameWithoutExtension(b) + ".*").Length == 0) {
                            try { File.Delete(b); } catch { }
                        }
                    }
                }
            });

            lblStatus.Text = "FINALIZADO CON ÉXITO";
            lblStatus.ForeColor = Color.LawnGreen;
            luzBarra.Width = 0;
            btnStart.Text = "INICIAR PROCESO";
            btnStart.BackColor = neonBlue;
            btnStart.ForeColor = Color.Black;
            btnStart.Enabled = true;
            
            CargarLogo();
            PlayCustomSound("CompletadoNE.wav");
            if (chkOpenDir.Checked) try { Process.Start(txtDst.Text); } catch { }
        }

        private void CargarLogo() { 
            try { 
                using (var s = GetResourceStream("dspico-logo.png")) { 
                    if (s != null) {
                        if (pbPreview.Image != null) pbPreview.Image.Dispose(); 
                        pbPreview.Image = Image.FromStream(s); 
                    }
                } 
            } catch { } 
        }

        private TextBox CreateTextBox(int y, int width) {
            TextBox t = new TextBox { Location = new Point(30, y), Size = new Size(width, 24), BackColor = darkInput, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9) };
            t.TextChanged += (s, e) => { if (!isUpdatingConfig) SaveConfig(); };
            this.Controls.Add(t); return t;
        }

        private Button CreateSmallFolderButton(int x, int y, string tag) {
            Button b = new Button { Text = "DIR", Location = new Point(x, y - 1), Size = new Size(35, 24), BackColor = btnGray, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Font = new Font("Segoe UI", 6, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => {
                using (FolderBrowserDialog f = new FolderBrowserDialog()) {
                    if (f.ShowDialog() == DialogResult.OK) {
                        if (tag == "src") txtSrc.Text = f.SelectedPath;
                        else if (tag == "dst") txtDst.Text = f.SelectedPath;
                        else if (tag == "roms") txtRoms.Text = f.SelectedPath;
                        SaveConfig();
                    }
                }
            };
            this.Controls.Add(b); return b;
        }

        private CheckBox CreateCheckBox(string txt, int x, int y) {
            CheckBox cb = new CheckBox { Text = txt, Location = new Point(x, y), Size = new Size(350, 25), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 8) };
            cb.CheckedChanged += (s, e) => { UpdateUI(); if (!isUpdatingConfig) { SaveConfig(); PlayCustomSound(cb.Checked ? "Select.wav" : "Quitar.wav"); } };
            return cb;
        }

        private Label AddLabel(string txt, int y) { Label l = new Label { Text = txt, Location = new Point(30, y), Size = new Size(400, 18), ForeColor = Color.Gray, Font = new Font("Segoe UI", 7, FontStyle.Bold) }; this.Controls.Add(l); return l; }

        private void UpdateUI() {
            bool c = chkClean.Checked;
            txtRoms.Enabled = btnBrowseRoms.Enabled = c;
            lblRomsTitle.ForeColor = c ? Color.Silver : Color.FromArgb(60, 60, 65);
            chkClean.ForeColor = chkClean.Checked ? neonBlue : Color.White;
            chkOpenDir.ForeColor = chkOpenDir.Checked ? neonBlue : Color.White;
            chkOverwrite.ForeColor = chkOverwrite.Checked ? neonBlue : Color.White;
        }

        private void SaveConfig() { try { File.WriteAllLines(configFile, new string[] { txtSrc.Text, txtDst.Text, txtRoms.Text, chkClean.Checked.ToString(), chkOverwrite.Checked.ToString(), chkOpenDir.Checked.ToString() }); } catch { } }

        private void LoadConfig() {
            if (File.Exists(configFile)) {
                string[] l = File.ReadAllLines(configFile);
                if (l.Length >= 6) {
                    txtSrc.Text = l[0]; txtDst.Text = l[1]; txtRoms.Text = l[2];
                    chkClean.Checked = l[3] == "True"; chkOverwrite.Checked = l[4] == "True"; chkOpenDir.Checked = l[5] == "True";
                }
            }
        }

        private void PlayCustomSound(string f) { 
            try { 
                using (var s = GetResourceStream(f)) { 
                    if (s != null) new SoundPlayer(s).Play(); 
                } 
            } catch { } 
        }

        [STAThread] static void Main() { Application.EnableVisualStyles(); Application.Run(new PicoLauncher()); }
    }
}
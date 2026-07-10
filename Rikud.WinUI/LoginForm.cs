using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using Rikud.WinUI.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using Rikud.WinUI.DLL;
using Rikud.WinUI.Helpers;

namespace Rikud.WinUI
{
    public partial class LoginForm : Form
    {

        public LoginForm()
        {
            InitializeComponent();
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {
            bool sonuc = true;
            var existUser = Helpers.CredentialHelper.Load();
            if (existUser != null)
            {
                var kullanicim = new RikudEntities().Kullanicilar.FirstOrDefault(x => x.Username == existUser.Username);

                if (kullanicim != null)
                {
                    byte[] storedSalt = AuthHasherHelper.FromBase64(existUser.SaltB64);
                    byte[] storedHash = AuthHasherHelper.FromBase64(kullanicim.PasswordHash);

                    byte[] hash = Helpers.AuthHasherHelper.ComputeHash(existUser.Password, Program.CpuId, Program.HddId, storedSalt);

                    bool ok = AuthHasherHelper.FixedTimeEquals(storedHash, hash);
                    if (ok)
                    {
                        this.Visible = false;
                        WebForm webForm = new WebForm();
                        webForm.Show();
                    }
                    else
                        sonuc = false;
                }
                else
                    sonuc = false;
            }

            if (!sonuc)
                if (DialogResult.OK == MessageBox.Show("Bu kullanıcı daha önce başka bir makinadan giriş yapmıştır.Lütfen IT niz ile iletişime geçiniz.", "UYARI", MessageBoxButtons.OK))
                    this.Close();

        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtUserName.Text.Trim()))
            {
                MessageBox.Show("Lütfen kullanıcı adınızı giriniz");
                txtUserName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(txtPassword.Text.Trim()))
            {
                MessageBox.Show("Lütfen şifrenizi giriniz");
                txtPassword.Focus();
                return;
            }

            var beniHatirla = chkRememberMe.Checked;


            byte[] salt = Helpers.AuthHasherHelper.GenerateSalt();

            byte[] hash = Helpers.AuthHasherHelper.ComputeHash(txtPassword.Text.Trim(), Program.CpuId, Program.HddId, salt);

            // 3) DB'ye sakla (örnek: Base64)
            string saltB64 = Helpers.AuthHasherHelper.ToBase64(salt);
            string hashB64 = Helpers.AuthHasherHelper.ToBase64(hash);

            var record = new CredentialRecord
            {
                Username = txtUserName.Text.Trim(),
                SaltB64 = saltB64,
                Password = txtPassword.Text.Trim(),
                Version = 1
            };

            Helpers.CredentialHelper.Save(record);

            //db işlemleri

            RikudEntities rikudEntities = new RikudEntities();
            Kullanicilar kullanicilar = new Kullanicilar();
            kullanicilar.Username = txtUserName.Text.Trim();
            kullanicilar.PasswordHash = hashB64;
            kullanicilar.Active = true;

            rikudEntities.Kullanicilar.Add(kullanicilar);
            rikudEntities.SaveChanges();

            this.Visible = false;
            WebForm webForm = new WebForm();
            webForm.Show();

        }

        private void LoginForm_FormClosed(object sender, FormClosedEventArgs e)
        {

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace Pivet.GUI
{
    internal class EncryptDialog : Dialog
    {
        internal string Result = "";
        TextField encryptedText;
        internal EncryptDialog() : base("Encrypt Text")
        {
            Height = 10;
            Width = 50;


            var label = new Label("Enter text: ")
            {
                Height = 1,
                Width = 13,
                X = 1,
                Y = 1
            };

            var sensitiveText = new TextField()
            {
                X = Pos.Right(label) + 1,
                Y = Pos.Top(label),
                Secret = true,
                Width = 20,
                Height = 1
            };

            sensitiveText.TextChanged += SensitiveText_TextChanged;

            Add(label, sensitiveText);


            label = new Label("Encrypted: ")
            {
                Height = 1,
                Width = 13,
                X = 1,
                Y = Pos.Bottom(sensitiveText) + 1
            };

            encryptedText = new TextField()
            {
                X = Pos.Right(label) + 1,
                Y = Pos.Top(label),
                ReadOnly = true,
                Width = 20,
                Height = 1
            };

            Add(label, encryptedText);

            var done = new Button("Done")
            {
                X = Pos.Center(),
                Y = Pos.AnchorEnd(1),
                Height = 1,
                Width = 8,
                TextAlignment = TextAlignment.Centered
            };

            done.Clicked += () =>
            {
                Application.RequestStop();
            };

            Add(done);
        }

        private void SensitiveText_TextChanged(NStack.ustring obj)
        {
            encryptedText.Text = PasswordCrypto.EncryptPassword(obj.ToString());
            Result = encryptedText.Text.ToString();
        }
    }
}

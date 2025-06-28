
namespace AddonManager.Forms
{
    public partial class ConsoleForm : Form
    {
        private readonly RichTextBox consoleOutput;

        public ConsoleForm()
        {
            InitializeComponent();
            consoleOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.LightBlue,
                Font = new Font("Consolas", 10),
                ReadOnly = true, // Prevent user from editing logs
                BorderStyle = BorderStyle.None
            };
            this.Controls.Add(consoleOutput);
            this.Text = "CONSOLE LOGS";
            RefreshLogs();
        }

        // Refreshes the console output with the latest logs, color-coding each log based on its level
        // This version is optimized for performance with large amounts of logs.
        public void RefreshLogs()
        {
            // Suspend drawing to prevent flickering and improve performance
            consoleOutput.SuspendLayout();
            consoleOutput.Clear();

            var logs = Logger.GetLogs();
            if (logs == null)
            {
                consoleOutput.ResumeLayout();
                return;
            }

            foreach (string log in logs)
            {
                if (string.IsNullOrEmpty(log)) continue;

                // Store current text length to calculate selection start later
                int selectionStart = consoleOutput.TextLength;
                consoleOutput.AppendText(log + Environment.NewLine);

                // Find the log level label brackets robustly
                int openBracketIndex = log.IndexOf('[');
                int closeBracketIndex = log.IndexOf(']');

                // Proceed only if a valid [LEVEL] tag is found
                if (openBracketIndex != -1 && closeBracketIndex > openBracketIndex)
                {
                    string level = log.Substring(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1);
                    Color levelColor;

                    switch (level)
                    {
                        case "INFO":
                            levelColor = Color.Cyan;
                            break;
                        case "WARN":
                            levelColor = Color.Yellow;
                            break;
                        case "ERROR":
                            levelColor = Color.Red;
                            break;
                        default:
                            levelColor = Color.White;
                            break;
                    }

                    // Select the [LEVEL] part and change its color
                    consoleOutput.Select(selectionStart + openBracketIndex, closeBracketIndex - openBracketIndex + 1);
                    consoleOutput.SelectionColor = levelColor;
                }
            }

            // Deselect all text and scroll to the end
            consoleOutput.Select(consoleOutput.TextLength, 0);
            consoleOutput.ScrollToCaret();

            // Resume drawing now that all updates are complete
            consoleOutput.ResumeLayout();
        }
    }
}
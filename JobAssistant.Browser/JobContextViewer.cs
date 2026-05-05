using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using JobAssistant.Core.Models;
using JobAssistant.Core.Services;

namespace JobAssistant.Browser;

internal sealed class JobContextViewer : IDisposable
{
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Thread _uiThread;
    private Exception? _startupException;
    private Form? _form;
    private bool _disposed;

    private JobContextViewer(string title, string content)
    {
        _uiThread = new Thread(() => RunViewer(title, content))
        {
            IsBackground = true,
            Name = "JobContextViewer",
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        _ready.Wait();

        if (_startupException is not null)
        {
            throw new InvalidOperationException("Could not open the job context window.", _startupException);
        }
    }

    public static JobContextViewer? Open(JobListing? job)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var content = JobContextFormatter.FormatForContextWindow(job);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var title = JobContextFormatter.GetWindowTitle(job);
        return new JobContextViewer(title, content);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_form is not null && !_form.IsDisposed && _form.IsHandleCreated)
            {
                _form.BeginInvoke(new MethodInvoker(() => _form.Close()));
            }
        }
        catch
        {
        }

        if (_uiThread.IsAlive)
        {
            _uiThread.Join(TimeSpan.FromSeconds(2));
        }

        _ready.Dispose();
    }

    private void RunViewer(string title, string content)
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var form = CreateForm(title, content);
            _form = form;
            _ready.Set();
            Application.Run(form);
        }
        catch (Exception exception)
        {
            _startupException = exception;
            _ready.Set();
        }
    }

    private static Form CreateForm(string title, string content)
    {
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(120, 120, 560, 760);
        var width = Math.Min(560, Math.Max(420, workingArea.Width / 3));
        var height = Math.Min(760, Math.Max(480, workingArea.Height - 120));
        var x = workingArea.Right - width - 20;
        var y = workingArea.Top + 20;

        var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(Math.Max(workingArea.Left, x), y),
            Size = new Size(width, height),
            MinimumSize = new Size(420, 420),
            TopMost = true,
            FormBorderStyle = FormBorderStyle.SizableToolWindow,
        };

        var textBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = true,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window,
            DetectUrls = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
            Text = content,
        };

        form.Controls.Add(textBox);
        return form;
    }
}
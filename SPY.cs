using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Core;
using HWND = System.IntPtr;
using HANDLE = System.IntPtr;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace SpyForm
{
    public partial class SPY : Form
    {

        public SPY()
        {
            InitializeComponent();
            _cursorDefault = Cursor.Current;
            _cursorFinder = LoadCursor(Finder);
            _finderHome = LoadImage(FinderHome);
            _finderGone = LoadImage(FinderGone);

            _pictureBox.Image = _finderHome;
            _pictureBox.MouseDown += new MouseEventHandler(OnFinderToolMouseDown);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();

                    if (_capturing)
                        this.CaptureMouse(false);
                }
            }
            base.Dispose(disposing);
        }
        #region 定义
        public delegate void DisplayImageEventHandler(Image image, bool autoDecideOnSizing, PictureBoxSizeMode manualSizeMode);
        public event DisplayImageEventHandler ImageReadyForDisplay;
        private bool _capturing;
        private Image _finderHome;
        private Image _finderGone;
        private Cursor _cursorDefault;
        private Cursor _cursorFinder;
        private IntPtr _hPreviousWindow;
        public const string FinderHome = "SpyForm.ICO.cross.bmp";
        public const string FinderGone = "SpyForm.ICO.blank.bmp";
        public const string Finder = "SpyForm.ICO.cursor.cur";
        #endregion
        /// <summary>
        /// Processes window messages sent to the Spy Window
        /// </summary>
        /// <param name="m"></param>
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                /*
                 * stop capturing events as soon as the user releases the left mouse button
                 * */
                case (int)User.WM_LBUTTONUP:
                    this.CaptureMouse(false);
                    break;
                /*
                 * handle all the mouse movements
                 * */
                case (int)User.WM_MOUSEMOVE:
                    this.HandleMouseMovements();
                    break;
            };

            base.WndProc(ref m);
        }
        /// <summary>
        /// Loads an image from an embbedded resource
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public static Image LoadImage(string resourceName)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    return Image.FromStream(stream);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            return null;
        }
        /// <summary>
        /// Loads a cursor from an embedded resource
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public static Cursor LoadCursor(string resourceName)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    return new Cursor(stream);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            return null;
        }
        /// <summary>
        /// Highlights the specified window just like Spy++
        /// </summary>
        /// <param name="hWnd"></param>
        public static void Highlight(IntPtr hWnd)
        {
            const float penWidth = 3;
            RECT rc = new RECT();
            User.GetWindowRect(hWnd, ref rc);

            IntPtr hDC = User.GetWindowDC(hWnd);
            if (hDC != IntPtr.Zero)
            {
                using (Pen pen = new Pen(Color.Black, penWidth))
                {
                    using (Graphics g = Graphics.FromHdc(hDC))
                    {
                        g.DrawRectangle(pen, 0, 0, rc.Right - rc.Left - (int)penWidth, rc.Bottom - rc.Top - (int)penWidth);
                    }
                }
            }
            User.ReleaseDC(hWnd, hDC);
        }
        /// <summary>
        /// Forces a window to refresh, to eliminate our funky highlighted border
        /// </summary>
        /// <param name="hWnd"></param>
        public static void Refresh(IntPtr hWnd)
        {
            User.InvalidateRect(hWnd, IntPtr.Zero, 1 /* TRUE */);
            User.UpdateWindow(hWnd);
            User.RedrawWindow(hWnd, IntPtr.Zero, IntPtr.Zero, User.RDW_FRAME | User.RDW_INVALIDATE | User.RDW_UPDATENOW | User.RDW_ALLCHILDREN);
        }
        /// <summary>
        /// Processes the mouse down events for the finder tool 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFinderToolMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                this.CaptureMouse(true);
        }
        /// <summary>
        /// Captures or releases the mouse
        /// </summary>
        /// <param name="captured"></param>
        private void CaptureMouse(bool captured)
        {
            // if we're supposed to capture the window
            if (captured)
            {
                // capture the mouse movements and send them to ourself
                User.SetCapture(this.Handle);

                // set the mouse cursor to our finder cursor
                Cursor.Current = _cursorFinder;

                // change the image to the finder gone image
                _pictureBox.Image = _finderGone;
            }
            // otherwise we're supposed to release the mouse capture
            else
            {
                // so release it
                User.ReleaseCapture();

                // put the default cursor back
                Cursor.Current = _cursorDefault;

                // change the image back to the finder at home image
                _pictureBox.Image = _finderHome;

                // and finally refresh any window that we were highlighting
                if (_hPreviousWindow != IntPtr.Zero)
                {
                    Refresh(_hPreviousWindow);
                    _hPreviousWindow = IntPtr.Zero;
                }
            }

            // save our capturing state
            _capturing = captured;
        }
        /// <summary>
        /// Handles all mouse move messages sent to the Spy Window
        /// </summary>
        private void HandleMouseMovements()
        {
            // if we're not capturing, then bail out
            if (!_capturing)
                return;
            try
            {
                int x = Cursor.Position.X;
                int y = Cursor.Position.Y;
                // capture the window under the cursor's position
                IntPtr hWnd = User.WindowFromPoint(x, y);

                // if the window we're over, is not the same as the one before, and we had one before, refresh it
                if (_hPreviousWindow != IntPtr.Zero && _hPreviousWindow != hWnd)
                    Refresh(_hPreviousWindow);

                // if we didn't find a window.. that's pretty hard to imagine. lol
                if (hWnd == IntPtr.Zero)
                {
                    this.IDC_EDIT1.Text = "";
                    this.IDC_EDIT2.Text = "";
                    this.IDC_EDIT3.Text = "";
                    this.IDC_EDIT4.Text = "";
                    this.IDC_EDIT5.Text = "";
                    this.IDC_EDIT6.Text = "";
                    this.IDC_EDIT7.Text = "";
                    this.IDC_EDIT8.Text = "";
                    this.IDC_EDIT9.Text = "";
                    this.IDC_EDIT10.Text = "";
                    this.IDC_EDIT11.Text = "";
                    this.IDC_EDIT12.Text = "";
                    this.IDC_EDIT13.Text = "";
                }
                else
                {
                    StringBuilder ch = new StringBuilder(128);
                    IntPtr PID = IntPtr.Zero, TID = IntPtr.Zero;
                    // save the window we're over
                    _hPreviousWindow = hWnd;
                    HWND hParent = User.GetParent(hWnd);
                    if ((int)hParent > 0)
                    {
                        IDC_EDIT9.Text = string.Format("{0}", hParent.ToString());
                        User.GetWindowText(hParent, ch, 128);
                        IDC_EDIT10.Text = string.Format("{0}", ch.ToString());
                        User.GetClassName(hParent, ch, 128);
                        IDC_EDIT11.Text = string.Format("{0}", ch.ToString());
                    }
                    else
                    {
                        IDC_EDIT9.Text = "N/A";
                        IDC_EDIT10.Text = "N/A";
                        IDC_EDIT11.Text = "N/A";
                    }
                    // handle
                    this.IDC_EDIT1.Text = string.Format("{0}", _hPreviousWindow.ToInt32().ToString());
                    // class
                    User.GetClassName(hWnd, ch, 128);
                    this.IDC_EDIT4.Text = string.Format("{0}", ch.ToString());
                    // control ID
                    int nID = User.GetWindowLong(hWnd, User.GWL_ID);
                    IDC_EDIT2.Text = string.Format("{0}", nID.ToString());
                    // caption
                    User.GetWindowText(hWnd, ch, 256);
                    this.IDC_EDIT3.Text = string.Format("{0}", ch.ToString());

                    RECT rc = new RECT();
                    User.GetWindowRect(hWnd, ref rc);

                    // rect
                    this.IDC_EDIT6.Text = string.Format("[{0} x {1}], ({2},{3})-({4},{5})", rc.Right - rc.Left, rc.Bottom - rc.Top, rc.Left, rc.Top, rc.Right, rc.Bottom);
                    this.IDC_EDIT12.Text = string.Format("{0},{1}", x, y);
                    this.IDC_EDIT13.Text = string.Format("{0},{1}", x-rc.Left, y-rc.Top);
                    //style

                    int style = User.GetWindowLong(hWnd, User.GWL_STYLE);
                    this.IDC_EDIT5.Text = string.Format("{0}", style.ToString());
                    //PID
                    if (Kernel.GetCurrentProcessId() == PID)
                        return;
                    //
                    TID = User.GetWindowThreadProcessId(hWnd, ref PID);
                    // thread ID
                    IDC_EDIT7.Text = string.Format("{0}", TID.ToString());
                    // process ID
                    IDC_EDIT8.Text = string.Format("{0}", PID.ToString());
                    // highlight the window
                    Highlight(hWnd);
                    if (Kernel.GetCurrentProcessId() != PID)
                    {
                        //Cursor.Current = _cursorDefault;
                        HANDLE hProcess =
                       Kernel.OpenProcess(User.PROCESS_CREATE_THREAD | User.PROCESS_QUERY_INFORMATION | User.PROCESS_VM_OPERATION | User.PROCESS_VM_WRITE | User.PROCESS_VM_READ,
                            0, (int)PID);

                        if (hProcess != null)
                        {
                            //g_hPwdEdit = hWnd;			// Initialize shared HWND (needed by "LibSpy.dll");

                            InjectDll(hProcess);		// Inject "LibSpy.dll" into the remote process;
                            Kernel.CloseHandle(hProcess);
                        }
                        else
                        {
                            User.SendMessage(hWnd, User.WM_GETTEXT, 128, IntPtr.Zero);
                            //Cursor.Current= _cursorDefault;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
        int InjectDll(HANDLE hProcess)
        {
            HANDLE hThread;
            StringBuilder szLibPath = new StringBuilder(Kernel.MAX_PATH);
            IntPtr pLibRemote = IntPtr.Zero;	// the address (in the remote process) where
            // szLibPath will be copied to;
            IntPtr hLibModule = IntPtr.Zero;	// base adress of loaded module (==HMODULE);

            IntPtr hKernel32 = Kernel.GetModuleHandle("Kernel32");
            // Get full path of "Core.dll"
            //if ((int)Kernel.GetModuleFileName(hInst, szLibPath, Kernel.MAX_PATH) == 0)
            if ((int)Kernel.GetModuleFileName(this.Handle, szLibPath, Kernel.MAX_PATH) == 0)
                return 0;
            //strcpy( strstr(szLibPath,".exe"),".dll" );

            // 1. Allocate memory in the remote process for szLibPath
            // 2. Write szLibPath to the allocated memory
            pLibRemote = Kernel.VirtualAllocEx(hProcess, IntPtr.Zero, szLibPath.Length + 1, Kernel.MEM_COMMIT, User.PAGE_READWRITE);
            if ((int)pLibRemote == 0)
                return 0;
            int lpNumberWritebuff = 0;
            Kernel.WriteProcessMemory(hProcess, pLibRemote, hKernel32, szLibPath.Length + 1, ref lpNumberWritebuff);


            // Load "Core.dll" into the remote process 
            // (via CreateRemoteThread & LoadLibrary)
            SECURITY_ATTRIBUTES SAT = new SECURITY_ATTRIBUTES();
            IntPtr lLoadAdress = Kernel.GetProcAddress(hKernel32, "LoadLibraryA");
            IntPtr ThredId = IntPtr.Zero;
            hThread = Kernel.CreateRemoteThread(hProcess, ref SAT, 0, ref lLoadAdress, pLibRemote, 0, ref ThredId);
            if ((int)hThread == 0)
                goto JUMP;

            Kernel.WaitForSingleObject(hThread, Kernel.INFINITE);

            // Get handle of loaded module
            Kernel.GetExitCodeThread(hThread, ref hLibModule);
            Kernel.CloseHandle(hThread);

        JUMP:
            Kernel.VirtualFree(hProcess, szLibPath.Length + 1, Kernel.MEM_RELEASE);
            if (hLibModule == null)
                return 0;


            // Unload "Core.dll" from the remote process 
            // (via CreateRemoteThread & FreeLibrary)
            IntPtr lFreeAdress = Kernel.GetProcAddress(hKernel32, "FreeLibrary");
            hThread = Kernel.CreateRemoteThread(hProcess, ref SAT, 0, ref lFreeAdress, hLibModule, 0, ref ThredId);
            if ((int)hThread == 0)	// failed to unload
                return 0;
            Kernel.WaitForSingleObject(hThread, Kernel.INFINITE);
            Kernel.GetExitCodeThread(hThread, ref hLibModule);
            Kernel.CloseHandle(hThread);

            // return value of remote FreeLibrary (=nonzero on success)
            return 1;
        }

    }
}

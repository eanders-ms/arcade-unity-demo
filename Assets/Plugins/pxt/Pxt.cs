using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

class ControllerState
{
    public enum PxtKeyCode
    {
        KEY_LEFT = 1,
        KEY_UP,
        KEY_RIGHT,
        KEY_DOWN,
        KEY_A,
        KEY_B,
        KEY_MENU,
        KEY_RESET = 100, // passed as event to TS, which does control.reset()
        KEY_EXIT,        // handled here
    };


    public enum PxtKeyState
    {
        IDLE = 0,
        INTERNAL_KEY_UP = 2050,
        INTERNAL_KEY_DOWN = 2051
    }

    int playerId;
    readonly Dictionary<PxtKeyCode, PxtKeyState> keys = new Dictionary<PxtKeyCode, PxtKeyState>();

    public ControllerState(int playerId)
    {
        this.playerId = playerId;
        keys[PxtKeyCode.KEY_LEFT] = PxtKeyState.IDLE;
        keys[PxtKeyCode.KEY_RIGHT] = PxtKeyState.IDLE;
        keys[PxtKeyCode.KEY_UP] = PxtKeyState.IDLE;
        keys[PxtKeyCode.KEY_DOWN] = PxtKeyState.IDLE;
        keys[PxtKeyCode.KEY_A] = PxtKeyState.IDLE;
        keys[PxtKeyCode.KEY_B] = PxtKeyState.IDLE;
    }
    public void UpdateKey(PxtKeyCode code, bool state)
    {
        if (state == false) // up
        {
            if (keys[code] == PxtKeyState.INTERNAL_KEY_DOWN)
            {
                keys[code] = PxtKeyState.INTERNAL_KEY_UP;
            }
            else
            {
                keys[code] = PxtKeyState.IDLE;
            }
        }
        else
        {
            keys[code] = PxtKeyState.INTERNAL_KEY_DOWN;
        }
    }

    public void Send()
    {
        foreach (var kv in keys)
        {
            if (kv.Value != PxtKeyState.IDLE)
            {
                Pxt.VmRaiseEvent((int)kv.Value, (int)kv.Key + 7 * playerId);
                Pxt.VmRaiseEvent((int)kv.Value, 0); // any
            }
        }
    }
}

public class Pxt : MonoBehaviour
{
    private const string DllPath = "pxt";
    private const int WIDTH = 160;
    private const int HEIGHT = 120;

    private byte[] data;
    private char[] logchrs;
    private IntPtr buffer;
    private IntPtr logbuf;
    private Texture2D texture;
    private Sprite sprite;

    private ControllerState player1 = new ControllerState(0);
    private ControllerState player2 = new ControllerState(1);

    public Pxt() {
        data = new byte[WIDTH * HEIGHT * 4];
        logchrs = new char[4096];
        buffer = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(int)) * data.Length);
        logbuf = Marshal.AllocCoTaskMem(4096);
        VmSetDataDirectory(".");
    }

    // Start is called before the first frame update
    void Start()
    {
        transform.position = new Vector3(0, 0, 1);
        texture = new Texture2D(WIDTH, HEIGHT, TextureFormat.BGRA32, false);
        sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));

        var sr = GetComponent<SpriteRenderer>();
        sr.sprite = sprite;

        //var path = Application.streamingAssetsPath + "/ROMs/binary.pxt64";
        //VmStart(path);

        var rom = Resources.Load<TextAsset>("roms/castle-crawler.pxt64");
        VmStartBuffer(rom.bytes, rom.bytes.Length);
    }

    // Update is called once per frame
    void Update()
    {
        float screenHeight = Camera.main.orthographicSize * 2.0f;
        float screenWidth = screenHeight * Camera.main.aspect;
        float height = screenHeight / sprite.bounds.size.y;
        float width = screenWidth / sprite.bounds.size.x;
        transform.localScale = new Vector3(width, -height, 1);

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            VmRaiseEvent(2051, 100); // key-down, reset
        }
        else
        {
            UpdateControllerStates();
            player1.Send();
            player2.Send();
        }

        VmGetPixels(WIDTH, HEIGHT, buffer);
        Marshal.Copy(buffer, data, 0, WIDTH * HEIGHT * 4);
        texture.LoadRawTextureData(data);
        texture.Apply(false);
    }

    void OnDestroy()
    {
        Marshal.FreeCoTaskMem(buffer);
        Marshal.FreeCoTaskMem(logbuf);
    }

    void UpdateControllerStates()
    {
        player1.UpdateKey(ControllerState.PxtKeyCode.KEY_UP, Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow));
        player1.UpdateKey(ControllerState.PxtKeyCode.KEY_LEFT, Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow));
        player1.UpdateKey(ControllerState.PxtKeyCode.KEY_DOWN, Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow));
        player1.UpdateKey(ControllerState.PxtKeyCode.KEY_RIGHT, Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow));
        player1.UpdateKey(ControllerState.PxtKeyCode.KEY_A, Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.Z));
        player1.UpdateKey(ControllerState.PxtKeyCode.KEY_B, Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.E));

        player2.UpdateKey(ControllerState.PxtKeyCode.KEY_UP, Input.GetKey(KeyCode.I));
        player2.UpdateKey(ControllerState.PxtKeyCode.KEY_LEFT, Input.GetKey(KeyCode.J));
        player2.UpdateKey(ControllerState.PxtKeyCode.KEY_DOWN, Input.GetKey(KeyCode.L));
        player2.UpdateKey(ControllerState.PxtKeyCode.KEY_RIGHT, Input.GetKey(KeyCode.K));
        player2.UpdateKey(ControllerState.PxtKeyCode.KEY_A, Input.GetKey(KeyCode.U));
        player2.UpdateKey(ControllerState.PxtKeyCode.KEY_B, Input.GetKey(KeyCode.O));
    }

    [DllImport(DllPath, EntryPoint = "pxt_screen_get_pixels", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern void VmGetPixels(int width, int height, IntPtr screen);

    [DllImport(DllPath, EntryPoint = "pxt_raise_event", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern void VmRaiseEvent(int src, int val);

    [DllImport(DllPath, EntryPoint = "pxt_vm_start", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern void VmStart([MarshalAs(UnmanagedType.LPStr)] string path);

    [DllImport(DllPath, EntryPoint = "pxt_vm_start_buffer", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern void VmStartBuffer(byte[] buffer, int size);

    [DllImport(DllPath, EntryPoint = "pxt_get_logs", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int VmGetLogs(int logtype, IntPtr dst, int maxSize);

    [DllImport(DllPath, EntryPoint = "pxt_vm_set_data_directory", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern void VmSetDataDirectory([MarshalAs(UnmanagedType.LPStr)] string path);



}

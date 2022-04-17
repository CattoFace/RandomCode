using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShaderCode : MonoBehaviour
{

    int Resolution;
    public ComputeShader shader;
    private RenderTexture texture;
    public bool debug;
    private ComputeBuffer pixelsBuffer;
    private float delta;
    private Dictionary<string,uint> colors;
    private int mouseCoordY;
    private int mouseCoordX;
    public uint gameStage;
    public float gameSpeed;
    public int evaporationConstant;
    private bool pacifist;
    private int whiteY;
    private uint[] scores;
    private ComputeBuffer scoreBuffer;
    private struct Pixel
    {
        #pragma warning disable 649
        public uint color;
        public int direction;
        public uint moved;
        #pragma warning restore 649

    }
    private Pixel[] pixels;

    private void OnApplicationQuit()
    {
        pixelsBuffer.Release();
        scoreBuffer.Release();
    }
    // Start is called before the first frame update
    void Start()
    {
        Resolution = 64;
        pixels = new Pixel[Resolution * Resolution];
        scores =new uint[]{20,20};
        scoreBuffer = new ComputeBuffer(scores.Length, sizeof(uint));
        scoreBuffer.SetData(scores);
        pixelsBuffer = new ComputeBuffer(pixels.Length, sizeof(uint)*2+sizeof(int));
        pixelsBuffer.SetData(pixels);
        delta = 0;
        mouseCoordY = -1;
        pacifist = true;
        whiteY = 0;
        colors = new Dictionary<string, uint>() {
            {"gray", 0 },{ "black", 1 },{"white", 2 },{"dark", 3 },{"bright", 4 },{"whiteEvaporator", 6 },{"blackEvaporator", 7 } };
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (texture == null)
        {
            shader.SetInt("Resolution", Resolution);
            shader.SetInt("evaporationConstant", evaporationConstant);
            texture = new RenderTexture(Resolution, Resolution, 24)
            {
                enableRandomWrite = true
            };
            texture.Create();

            int kernel = shader.FindKernel("Initiate");
            shader.SetBuffer(kernel, "pixels", pixelsBuffer);
            shader.Dispatch(kernel, Resolution / 8, Resolution / 8, 1);

            kernel = shader.FindKernel("Update");
            shader.SetBuffer(kernel, "scores", scoreBuffer); 
            shader.SetBuffer(kernel, "pixels", pixelsBuffer);

            kernel = shader.FindKernel("Render");
            shader.SetTexture(kernel, "Result", texture);
            shader.SetBuffer(kernel, "pixels", pixelsBuffer);
        }
        else
        {
            if (gameStage > 0)
            {
                delta += Time.deltaTime;
                mouseCoordY = (int)(System.Math.Floor(Input.mousePosition.y) / Screen.height * Resolution);
                mouseCoordX = (int)(System.Math.Floor(Input.mousePosition.x) / Screen.width * Resolution);
                shader.SetInt("frame", Time.frameCount);
                shader.SetInt("mouseY", mouseCoordY);
                shader.SetInt("mouseX", mouseCoordX);
                shader.SetInt("whiteY", whiteY);
                if (delta >= 1 / gameSpeed)
                {
                    delta = 0;
                    BlackPlay();
                    WhitePlay();
                    pixelsBuffer.SetData(pixels);
                    scoreBuffer.SetData(scores);
                    shader.Dispatch(shader.FindKernel("Update"), Resolution / 8, Resolution / 8, 1);
                    scoreBuffer.GetData(scores);
                    if (gameStage == 2 || gameStage == 4)
                    {
                        gameSpeed = Math.Min(gameSpeed + 1, 500);
                        if (gameSpeed == 500)
                            evaporationConstant = Math.Max(evaporationConstant - 5,20);
                            shader.SetInt("evaporationConstant", evaporationConstant);
                    }
                }
            }
        }
        shader.Dispatch(shader.FindKernel("Render"), Resolution / 8, Resolution / 8, 1);
        pixelsBuffer.GetData(pixels);
        texture.filterMode = FilterMode.Point;
        Graphics.Blit(texture, dest);
    }

    private void BlackPlay()
    {
        if (gameStage > 0 && ValidCoords(mouseCoordX, mouseCoordY))
        {
            if (Input.GetMouseButton(0) && pixels[mouseCoordX * Resolution + mouseCoordY].color == 0)
            {
                if (scores[0] >= 1)
                {
                    scores[0] -= 1;
                    pixels[mouseCoordX * Resolution + mouseCoordY].color = colors["black"];
                }
            }
            if (Input.GetMouseButton(1) && pixels[mouseCoordX * Resolution + mouseCoordY].color == 0)
            {
                if (scores[0] >= 5)
                {
                    scores[0] -= 5;
                    pixels[mouseCoordX * Resolution + mouseCoordY].color = colors["whiteEvaporator"];
                    pacifist = false;
                }
            }
            for (int i = -1; i <= 1; i++)
            {
                if (ValidCoords(mouseCoordX,mouseCoordY+i)&&pixels[mouseCoordX * Resolution + mouseCoordY+i].color == colors["blackEvaporator"])
                {
                    pixels[mouseCoordX * Resolution + mouseCoordY+i].color = colors["gray"];
                }
            }
        }

    }

    private void WhitePlay()
    {
            uint color = (uint)(pacifist ? 2 : 7);
            uint cost = (uint)(pacifist ? 1 : 5);
        if (scores[1] >= cost)
        {
            uint wg = 0;
            foreach (Pixel p in pixels)
            {
                switch (p.color)
                {
                    case 0:
                        wg++;
                        break;
                    case 2:
                        wg++;
                        break;
                }
            }
            if (wg > 0)
            {
                uint pixelIndex = (uint)(UnityEngine.Random.Range(0, wg / 2) + UnityEngine.Random.Range(0, wg / 2) + 1);
                for (uint i = 0; i < pixels.Length; i++)
                {
                    switch (pixels[i].color)
                    {
                        case 0:
                            pixelIndex--;
                            break;
                        case 2:
                            pixelIndex--;
                            break;
                    }
                    if (pixelIndex == 0)
                    {
                        scores[1] -= cost;
                        pixels[i].color = color;
                        return;
                    }
                }
            }
        }        
    }

    // Update is called once per frame
    void Update()
    {
        uint w = 0;
        uint b = 0;
        uint g = 0;
        GameObject.FindGameObjectWithTag("BlackPointsText").GetComponentInChildren<Text>().text = $"Black Points: {scores[0]}";
        foreach (Pixel p in pixels)
        {
            switch (p.color)
            {
                case 0:
                    g++;
                    break;
                case 1:
                    b++;
                    break;
                case 2:
                    w++;
                    break;
            }
        }
        if (w <= Resolution * Resolution / 20)
        {
            uint[] toEliminate = { colors["white"], colors["blackEvaporator"]};
            Eliminate(toEliminate);
        }
        if(b <= Resolution * Resolution / 20)
        {
            uint[] toEliminate = { colors["black"], colors["whiteEvaporator"]};
            Eliminate(toEliminate);
        }
        if(g <= Resolution * Resolution / 20)
        {
            EliminateGray();
        }

        //ending 1
        if (w == 0 && gameStage==1)
        {
            gameStage = 2;
            Text text = GameObject.FindGameObjectWithTag("Ending1").GetComponentInChildren<Text>();
            Color color = text.color;
            color.a = 1;
            text.color = color;
        }
        if (gameStage == 2 && b == 0)
        {
            gameStage = 3;
        }
        if (gameStage == 3)
        {
            Image image = GameObject.FindGameObjectWithTag("Ending1_2").GetComponentInChildren<Image>();
            Color color = image.color;
            color.a = Mathf.Lerp(image.color.a, 1, Time.deltaTime / 20);
            image.color = color;
            Text text = GameObject.FindGameObjectWithTag("Ending1_2Text").GetComponentInChildren<Text>();
            color = text.color;
            color.a = Mathf.Lerp(text.color.a, 1, Time.deltaTime/5);
            text.color = color;
        }

        //ending 2
        if(b==0 & gameStage == 1)
        {
            gameStage = 4;
            Text text = GameObject.FindGameObjectWithTag("Ending2").GetComponentInChildren<Text>();
            Color color = text.color;
            color.a = 1;
            text.color = color;
        }
        if (gameStage == 4 && w == 0)
        {
            gameStage = 5;
        }
        if (gameStage == 5)
        {
            Text text = GameObject.FindGameObjectWithTag("Ending2_2").GetComponentInChildren<Text>();
            Color color = text.color;
            color.a = Mathf.Lerp(text.color.a, 1, Time.deltaTime/3);
            text.color = color;
        }

        //ending3
        if (g == 0 & gameStage == 1 && pacifist)
        {
            gameStage = 6;
        }
        if (gameStage == 6) { 
            Text text = GameObject.FindGameObjectWithTag("Ending3").GetComponentInChildren<Text>();
            Color color = text.color;
            color.a = Mathf.Lerp(text.color.a, 1, Time.deltaTime/3);
            text.color = color;
        }

        if (debug)
        {
            print($"Black: {b}, White: {w}, Gray: {g}, Game Stage: {gameStage}, Pacifist: {pacifist}, White Points: {scores[1]}");
        }
        
        if (Input.GetKeyDown(KeyCode.Return)&&gameStage==0)
        {
            GameObject.FindGameObjectWithTag("Title").SetActive(false);
            gameStage = 1;
        }
    }

    private void EliminateGray()
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].color == colors["gray"])
            {
                pixels[i].color = i > Resolution * Resolution / 2 ? colors["white"] : colors["gray"];
            }
        }
    }

    private void Eliminate(uint[] toEliminate)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            if (toEliminate.Contains(pixels[i].color))
            {
                pixels[i].color = colors["gray"];
            }
        }
    }

    bool ValidCoords(int x, int y)
    {
        bool ans = false;
        if (x >= 0 && y >= 0 && x < Resolution && y < Resolution)
        {
            ans = true;
        }
        return ans;
    }
}

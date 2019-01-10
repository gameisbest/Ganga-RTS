﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AccidentalNoise;

public class TerrainGenerator : MonoBehaviour
{
    public int layersCount = 1;
    public FractalType fractalType = FractalType.MULTI;
    public BasisTypes basisType = BasisTypes.SIMPLEX;
    public InterpTypes interpType = InterpTypes.QUINTIC;

    public int octaves = 3;
    public double frequency = 2.0;
    public double lacunarity = 2.0;
    public uint seed = 1;

    public Texture2D mapTexture;

    public bool generate = false;
    float[,] mapData;

    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (generate)
        {
            generate = false;
            Generate((int)seed);
        }
    }

    public ModuleBase GetFractal()
    {
        Fractal ground_shape_fractal = new Fractal(fractalType,
                                            basisType,
                                            interpType,
                                            octaves,
                                            frequency,
                                            seed);
        ground_shape_fractal.SetLacunarity(lacunarity);
        return ground_shape_fractal as ModuleBase;
    }

    private float KeepPositive(float value)
    {
        if (value < 0) return 0.0f;
        if (value > 1) return 1.0f;
        return value;
    }

    public Color ParseHEX(string hexString)
    {
        Color newColor = new Color();
        ColorUtility.TryParseHtmlString(hexString, out newColor);
        return newColor;
    }

    private Vector2 CalculatePosition(Vector2 position)
    {
        Terrain t = Terrain.activeTerrain;
        float scaleX = t.terrainData.alphamapHeight / t.terrainData.size.x;
        float scaleY = t.terrainData.alphamapWidth / t.terrainData.size.z;
        return new Vector2(position.x * scaleX, position.y * scaleY);
    }

    public void SetTextureOnTerrain(Vector2 position, Vector2 size, int layer, int value)
    {
        Terrain t = Terrain.activeTerrain;
        float[,,] map = new float[(int)size.x, (int)size.y, layersCount];
        for (int y = 0; y < (int)size.x; y++)
        {
            for (int x = 0; x < (int)size.y; x++)
            {
                map[y, x, layer] = value;
            }
        }
        Vector2 terrainPosition = CalculatePosition(position);
        t.terrainData.SetAlphamaps((int)terrainPosition.x, (int)terrainPosition.y, map);
    }

    public void RemoveGrassOnTerrain(Vector2 position, Vector2 size)
    {
        Terrain t = Terrain.activeTerrain;
        int[,] grassLayers = new int[(int)size.x, (int)size.y];
        for (int y = 0; y < (int)size.x; y++)
        {
            for (int x = 0; x < (int)size.y; x++)
            {
                grassLayers[y, x] = 0;
            }
        }
        Vector2 terrainPosition = CalculatePosition(position);
        for(int i = 0; i <= 3; i++)
            t.terrainData.SetDetailLayer((int)terrainPosition.x, (int)terrainPosition.y, i, grassLayers);
    }

    public Dictionary<string, List<Vector3>> GetSpawnData(int spawnCount)
    {
        Dictionary<string, List<Vector3>> newData = new Dictionary<string, List<Vector3>>();
        int sizeX = Terrain.activeTerrain.terrainData.alphamapWidth;
        int sizeY = Terrain.activeTerrain.terrainData.alphamapHeight;

        newData["spawn"] = new List<Vector3>();
        for (int i = 0; i <= spawnCount; i++)
        {
            newData["spawn"].Add(new Vector3(100, 0, 100));
        }

        return newData;
    }

    public void Generate(int mapSeed)
    {
        seed = (uint)mapSeed;
        Terrain t = Terrain.activeTerrain;
        int sizeX = t.terrainData.alphamapWidth;
        int sizeY = t.terrainData.alphamapHeight;
        if (t.terrainData.detailHeight != t.terrainData.alphamapWidth)
            Debug.Log("detailHeight and alphamapWidth must be equal");
        mapData = new float[sizeX, sizeY];

        ModuleBase moduleBase = GetFractal();
        mapTexture = new Texture2D(sizeX, sizeY);
        SMappingRanges ranges = new SMappingRanges();

        float[,,] map = new float[sizeX, sizeY, layersCount];
        int[][,] grassLayers = new int[][,] { new int[sizeX, sizeY], new int[sizeX, sizeY], new int[sizeX, sizeY], new int[sizeX, sizeY] };
        for (int y = 0; y < sizeX; y++)
        {
            for (int x = 0; x < sizeY; x++)
            {
                double p = (double)x / (double)sizeX;
                double q = (double)y / (double)sizeY;
                double nx, ny = 0.0;
                nx = ranges.mapx0 + p * (ranges.mapx1 - ranges.mapx0);
                ny = ranges.mapy0 + q * (ranges.mapy1 - ranges.mapy0);

                float val = (float)moduleBase.Get(nx * scale, ny * scale);
                mapData[x, y] = val;

                float textureScale = (val + 1.0f);
                if (textureScale > 0.89f)
                     mapTexture.SetPixel(y, x, ParseHEX("#005C01"));
                else if (textureScale > 0.7f)
                    mapTexture.SetPixel(y, x, ParseHEX("#007501"));
                else if (textureScale < 0.2f)
                    mapTexture.SetPixel(y, x, ParseHEX("#3F8541"));
                else if (textureScale < 0.1f)
                    mapTexture.SetPixel(y, x, ParseHEX("#5F8560"));
                else
                    mapTexture.SetPixel(y, x, ParseHEX("#008501"));
                // mapTexture.SetPixel(x, y, new Color(val, val, val));

                float grassValue = 0.0f;
                if(textureScale > 0.75)
                    grassValue = KeepPositive((textureScale - 0.75f) * 4.0f);
                map[x, y, 1] = grassValue;

                float stoneValue = 0.0f;
                if (textureScale < 0.25)
                {
                    stoneValue = 1.0f - KeepPositive(textureScale * 4.0f);
                }
                map[x, y, 3] = stoneValue;

                map[x, y, 0] = KeepPositive(textureScale + 0.2f) - grassValue - stoneValue / 2.0f;
                map[x, y, 2] = KeepPositive(1.0f - textureScale - 0.2f) - grassValue - stoneValue / 2.0f;

                if (textureScale > 0.9f)
                {
                    grassLayers[3][x, y] = 1;
                }
                if (textureScale > 0.7f)
                {
                    grassLayers[2][x, y] = 2;
                }
                else if (textureScale > 0.5f)
                {
                    grassLayers[1][x, y] = 2;
                }   
                else if (textureScale > 0.15f)
                {
                    grassLayers[1][x, y] = 1;
                }
            }
        }
        mapTexture.Apply();
        t.terrainData.SetAlphamaps(0, 0, map);
        for (int i = 0; i < grassLayers.Length; i++)
            t.terrainData.SetDetailLayer(0, 0, i, grassLayers[i]);
    }
    public double scale = 1.0;

    public static double DoubleLerp(double start, double end, double amount)
    {
        double difference = end - start;
        double adjusted = difference * amount;
        return start + adjusted;
    }

    public static Color ColorLerp(Color colour, Color to, double amount)
    {
        // start colours as lerp-able floats
        double sr = colour.r, sg = colour.g, sb = colour.b;

        // end colours as lerp-able floats
        double er = to.r, eg = to.g, eb = to.b;

        // lerp the colours to get the difference
        float r = (float)(DoubleLerp(sr, er, amount) / 255.0),
             g = (float)(DoubleLerp(sg, eg, amount) / 255.0),
             b = (float)(DoubleLerp(sb, eb, amount) / 255.0);

        // return the new colour
        return new Color(r, g, b);
    }
}
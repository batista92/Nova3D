# CityBenchmark

Baseline permanente do renderer Nova3D. Esta cena deve continuar executável
durante a extração do protótipo e após mudanças relevantes no renderer.

## Carga de referência

| Sistema | Carga |
| --- | ---: |
| Terrain | 2048 x 2048 |
| CSM | 4 cascatas de 4096 x 4096 |
| Árvores | 10.000 |
| Prédios | 1.000 |
| Veículos | 500 |
| Água | 1 superfície |
| Pós-processamento | HDR, bloom e FXAA |

## Referência registrada no Gate #2

- aproximadamente 302 FPS;
- aproximadamente 3,31 ms por frame;
- aproximadamente 748 draw calls na captura de aprovação.

Esses números são uma referência de regressão, não uma meta portátil: resolução,
GPU, driver, câmera e quantidade visível alteram o resultado. Uma comparação
válida deve usar a mesma máquina, resolução, câmera e configuração.

O título da janela apresenta FPS, tempo de frame, visibilidade, draw calls reais,
triângulos do passe principal e draw calls das sombras.

## Execução

```powershell
dotnet run --project .\CityBuilder.csproj -c Release
```

`F1` alterna os modos de diagnóstico das cascatas de sombra.

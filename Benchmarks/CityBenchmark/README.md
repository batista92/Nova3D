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
dotnet run --project .\Benchmarks\Nova3D.Benchmarks\Nova3D.Benchmarks.csproj -c Release
```

`F1` alterna os modos de diagnóstico das cascatas de sombra.
## Captura determinística

```powershell
dotnet run --project .\Benchmarks\Nova3D.Benchmarks\Nova3D.Benchmarks.csproj `
  -c Release -- --capture .\artifacts\visual-captures\city
```

G8.3 adds two focused capture modes to the same host:

```powershell
dotnet run --project .\Benchmarks\Nova3D.Benchmarks\Nova3D.Benchmarks.csproj `
  -c Release -- --capture .\artifacts\visual-captures\pbr --scene Pbr
dotnet run --project .\Benchmarks\Nova3D.Benchmarks\Nova3D.Benchmarks.csproj `
  -c Release -- --capture .\artifacts\visual-captures\gltf --scene Gltf
```

`Pbr` isolates metallic/roughness materials and the four-cascade shader path.
`Gltf` imports generated deterministic GLB fixtures and draws one static and one
skinned animated primitive, including both shadow techniques. No external model
download is required. Run all committed comparisons with
`eng/run-visual-regression.ps1` on the controlled graphics machine.

Esse modo usa resolução 1280×720, câmera e timestep fixos, seed `9127`, desliga
MSAA e entrada interativa, aquece 120 frames, salva o frame 121 e encerra. O PNG
e o `.capture.json` ficam no diretório informado.

## Regressão de performance

```powershell
.\eng\run-performance-regression.ps1
```

O gate aquece 180 frames e mede 240 frames fixos em 1280x720 sem MSAA. O
relatório legível por IA fica em `artifacts/performance/city-benchmark.json` e
é validado contra `Benchmarks/PerformanceBudgets/city-benchmark.json`. O script
também preserva a regressão física headless; use `-SkipPhysics` apenas quando
ela já tiver sido executada separadamente. Comparações em outro hardware são
evidência auxiliar e geram aviso explícito.

# Controles

- `WASD`, `Q/E`: mover a câmera;
- botão direito + mouse: olhar ao redor;
- `Shift`: movimento rápido;
- `F1`: debug das cascatas de sombra;
- `F2`: deformação runtime do terreno;
- `F3`: geometria de debug;
- `F4`: alternar entre o benchmark da cidade e a galeria de validação GLB.

A cidade procedural inicia ativa. A galeria procura arquivos locais em
`LocalAssets/Models/validation` e pode ser aberta com `F4`. Essa pasta não faz
parte do Git. O título mostra quantos GLBs foram carregados e quantos falharam;
detalhes ficam em `Logs/nova3d-benchmarks.log`.

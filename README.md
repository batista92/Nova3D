# CityBuilder — pesquisa MonoGame

Projeto DesktopGL mínimo para executar as provas de conceito descritas no
[`ROADMAP.md`](ROADMAP.md). Nesta fase, o objetivo é validar o MonoGame antes de
criar abstrações de engine.

## Requisitos

- .NET 8 SDK ou superior
- OpenGL 3.3+ (ou o backend suportado pelo MonoGame DesktopGL na plataforma)

## Executar

```powershell
dotnet tool restore
dotnet run
```

A aplicação abre atualmente a prova de conceito de vegetação instanciada. A base
PBR, terrain e iluminação continua disponível nos testes anteriores.
Na inicialização, um céu HDR procedural gera:

- cubemap do ambiente e skybox;
- irradiance map para iluminação difusa;
- cubemap prefiltrado com mipmaps para reflexos por rugosidade;
- BRDF LUT para o split-sum specular IBL.

- Arraste com o botão esquerdo do mouse para orbitar a câmera.
- Use a roda do mouse para aproximar ou afastar.
- Pressione `Esc` para sair.
- O título mostra instâncias visíveis, distribuição por LOD e draw calls.
- Arraste com o botão esquerdo para orbitar e use a roda para testar os LODs.

## Próximo marco

Validar 10.000 árvores com GPU instancing, frustum culling e três níveis de LOD.

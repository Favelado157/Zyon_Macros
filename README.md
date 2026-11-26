# GhosttzzKBot

Painel nativo para Windows 11 x64 para gravar e reproduzir movimentos, cliques, rolagem e teclas. O instalador inclui o runtime e cria atalhos no menu Iniciar e na Área de Trabalho.

| Ação | Atalho inicial |
| --- | --- |
| Gravar / concluir a gravação | F5 |
| Pausar / retomar a gravação | F6 |
| Play | F7 |
| Parar | F8 |

Edite os atalhos no painel **Atalhos de Teclado**. Use **Salvar Gravação** para guardar um arquivo `.ghost.json`. Ao reproduzir, deixe a janela de destino em primeiro plano e use a mesma distribuição de monitores. A reprodução começa 400 ms após o comando. Pressione **Parar** ou o atalho inclusive em outra janela. A captura ocorre durante uma gravação iniciada pelo usuário. Desative **Gravar teclas do teclado** se quiser usar somente o mouse.

## Personalização

O botão de engrenagem superior abre `%LOCALAPPDATA%\GhosttzzKBot\config`. Substitua `background.png` e reabra o aplicativo, ou use **Trocar Imagem**. O arquivo `settings.json` guarda os atalhos, as opções de gravação e o repositório consultado para atualizações. Feche o painel antes de editar o JSON. O instalador mantém essa pasta quando atualiza o programa.

## Compilar e publicar

No Windows, com SDK .NET 8 e NSIS instalados, execute a partir desta pasta:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o publish
& "${env:ProgramFiles(x86)}\NSIS\makensis.exe" installer.nsi
```

O fluxo **Instalador Windows** compila um instalador no GitHub Actions. Uma tag como `v2.0.0` publica também o instalador no GitHub Releases. Para distribuir uma atualização da versão inicial, publique uma versão superior, como `v2.0.1`. O painel instalado verifica o lançamento estável mais recente ao abrir e pergunta se o usuário quer baixar o novo instalador. Durante a atualização, feche o painel quando solicitado e execute o instalador baixado. Se hospedar o projeto em outro repositório, ajuste `UpdateRepository` em `Settings.cs` para novas instalações e em `settings.json` para instalações existentes.

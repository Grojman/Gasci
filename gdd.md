# GDD de Relato

## Estructura de carpetas

- assets:
    - images:
        - characteres
        - general
    - data:
        - maps:
        - dialogs.csv

## Historia

Una persona vive su día a día mientras se enfrenta a seres monstruosos que representan los traumas que tiene. Conforme los va enfrentando su mundo va cambiando para más distorsionado o no, y la manera que tiene de afrontar estos traumas hará que tome una u otra decisión con las personas reales de su mundo mientras poco a poco va perdiendo la cabeza.

### Resumen

### Personajes

## Estilo

Las imágenes tendrán un estilo de ASCII art de terror, los personajes serán criaturas de terror análogo, deformes y dependiendo de lo que sea que representen.

## Mecánicas

### Finales con variaciones

Se va a mantener una misma idea que en los videojuegos de múltiples finales, como The Walking Dead. La historia y la jugabilidad presentará diferencias en base a lo que el usuario haga. Esto afecta tanto a las decisiones finales, "cinemáticas", como a pequeños diálogos que luego puedan o no mmodificar el final en mayor o menor medida.

### Elección de decisiones

Conforme el usuario vaya encontrándose diálogos por el juego, en muchos de ellos existirá una serie de opciones en las que tendrá que tomar una decisión para responder a los diálogos; hay algunos diálogos que, aunque se definan como tal, pueden ser acciones que el "personaje" ejecute. Este tipo de acciones supondrán cambios y nuevos eventos que continuaŕan para el resto de la partida.

### Movimiento por el mapa

Los mapas no van a estar limitados más allá de su propia estructura. Eso quiere decir que el usuario podrá ignorar eventos que den lugar a otros finales diferentes del juego. 

## Implementaciones

Se va a utilizar SadConsole para desarrollar el videojuego. El juego tendrá en total una serie de pantallas:

### Pantalla inicial

Con el titulo del juego arriba y justo abajo en el centro la serie de opciones:
- Continuar con la partida
- Empezar una nueva partida desde cero
- Acceder a los ajustes
- Salir del juego

En caso de que se salga del juego, la pantalla comenzará a llenarse de bits en negro hasta cubrirla completamente, y entonces se cerrará el juego.

### Ajustes

Se podrá modificar el volumen del juego, tanto la música como los efectos de sonido, y el idioma

### Pantalla principal: navegación

En esta pantalla se mostrará tanto el mapa actual como el jugador, el cual se podrá mover por este. Esta pantalla ocupará la totalidad de la pantalla, y puede ocurrir que se habran cuadros de diálogos que bloqueen el movimiento del jugador.

### Pantalla secundaria: dialogo

En esta pantalla habrán dos consolas: la primera se encontrará arriba y ocupará aproximadamente un 70% de la pantalla. En esta se dibujará un dibujo del personaje con el que el jugador se haya encontrado.

En la pantalla inferior se encontrarán las opciones que el jugador podrá seleccionar para responder ante la persona/criatura con la que esté interactuando.

Puede suceder que en mitad de una decisión las imágenes cambien, o se bloquee el acceso al jugador a una de las opciones.

### Pantalla terciaria: en negro

Será una pantalla personalizada que no cargará ningún mapa ni nada y ocupará el total de la pantalla. Servirá para dar un golpe de efecto como escribir una sola línea de texto en el centro de la pantalla, o una imagen, dependiendo de lo que requiera el juego en ese momento.


### Recreación de las imágenes.

Para las imágenes en específico se necesitará una clase que actúe como puntero. Esta clase recibirá información de archivos de texto donde se encontrarán los dibujos ASCII e irá dibujándolos en la pantalla carácter a carácter. Sin embargo, tendrá un enumerado que permita especificar cómo se tendrá que dibujar exactamente estas imágenes, con las siguientes opciones:
- Aleatorio: se van dibujando carácteres aleatorios sin ningún tipo de orden.
- Bordes: se comienza por los bordes de la imagen y termina llegando al centro de esta.
- Arriba a abajo: se dibuja linealmente, comenzando por la primera línea de arriba y terminando abajo del todo.
- Zig zag: Dividirá la imagen en dos partes: arriba y abajo. Primero dibujará un pixel de arriba, después uno de abajo... y así hasta que complete la imagen, escogiendo dentro de cada grupo de manera aleatoria.
- Interior: reverso de bordes. empezará en el centro de la imagen y continuará expandiéndose.

Este tipo de cursores dibujarán primero el carácter correspondiente a un cursor de consola típico, antes de escribir el que de verdad tengan que poner en el hueco. El cursor deberá ser capaz de desechar aquellos carácteres que ya coincidan con lo que se encuentra actualmente escrito en la consola, de manera que solo se necesita modificar los que hayan cambiado.

### Música y efectos de sonido

Habran en total tres tipos de categorías para los sonidos:
- Música: Tracks largos y continuados que consistirán en melodías que sonarán en diferentes partes del juego. Pueden ser también sonidos continuados ej: el sonido de un viento arreciando.
- Efectos de sonido: Sonidos cortos y rápidos que se ejecutan ante ciertas acciones.
- Cursores: cada vez que un cursor escribe un carácter, independientemente del tipo de cursor que sea, deberá reproducir un sonido concreto o uno aleatorio de entre un array de sonidos (dependiendo de la implementación, se asume que los sonidos tendrán una aleatoriedad en el tono para que no se escuche siempre la misma nota)

### Cuadros de diálogo

Tanto los cuadros que aparecen en la pantalla de dialogo como en el mapa deberán ser gestionados de cierta manera. Para evitar problemas lingüisticos, los cuadros de diálogo recivirán una cadena de texto que contendrá una clave, la cual usarán para obtener el texto verdadero de un servicio encargado de ello. El cuadro de texto deberá ser capaz de comprobar cuánto espacio disponible hay en dicho cuadro de texto, y en caso de que no haya sido capaz de introducir todo el texto, añadir un último icono para indicar que hay más por mostrar. Deberá esperar a la llamada de una función suya para poder limpiar el contenido del texto que había antes y mostrar el nuevo.

Este cuadro de texto debería aceptar diferentes estilos a la hora de escribirse los textos, Ej: velocidad variada para cada carácter, diferentes tamaños de letra... dependerá de qué permite SadConsole en estos casos.

El servicio mencionado tan solo obtendrá la información necesaria de un archivo csv que contendrá la totalidad de los textos mencionados.

### Imágenes

Deberá haber un servicio que en base a una clave permita obtener la información de una imágen. Al tratarse de estilos de ASCII, las imágenes serán archivos de texto en los que se encontrará escrita la imágen. La estructura de carpetas es la siguiente:
- assets
    - images
        - characters: para los personajes
            - <nombre personaje> : esto será una carpeta, dentro pueden haber archivos enumerados (1.txt, 2.txt...)
        - general: para imágenes genéricas, como por ejemplo algún escenario


También es necesario comprobar el sistema de imágenes de SadConsole para ver cuán necesario es este sistema, aunque el concepto de imágen ASCII se tiene que mantener.

### Mapa

Será un sistema muy parecido al de las imágenes, salvo que con información extra. Los mapas, además de estar directamente escritos en archivos txt, tendrán otro archivo que será <nombre>_info.json en el que se especificará una serie de información. Esta información consiste en identificar la utilidad de los distintos carácteres que se muestran dentro del archivo. Aquellos carácteres que no aparezcan en este archivo se considerarán secciones "pisables" por parte del usuario. Se incluirá, por tanto, en el json un array de objetos en los que se espefique el caracter y las propiedades de este, incluyendo el caso en el que sean interactuables.

Es posible que se intenten crear "animaciones" o movimientos de objetos dispuestos en el mapa. Se deberá comprobar con SadConsole si existen alternativas o no.

De una manera parecida a los cuadros de diálogo, se debe de gestionar en la pantalla del mapa el espacio disponible. Por tanto, los mapas deberán ser fraccionados en bloques en caso de que no ocupen el suficiente espacio en la pantalla, incluyendo iconos que indiquen que se puede seguir por la dirección en la que se encuentre la información que aun no se haya mostrado. Los mapas deberán controlar la transposición del usuario en el momento en el que se cargue la nueva sección. También existirá la opción de cargar una zona concreta de un mismo mapa (esto será utilizado para el sistema de guardado)

### Sistema de acciones

Este juego es una suerte de novela gráfica con un poco de exploración. Por tanto, las deciones que el jugador tome deben de ser almacenadas.

Por un lado, existirá un motor de información y gestión. Este motor es quien almacenará todas las variables que se encarguen de controlar las acciones pasadas y futuras del juego. Cuando una de estas variables se actualize, deberá repasar todos los eventos y sus condiciones, para poder comprobar si hay alguno que deba enviarse o eliminarse.

Por otro lado, existirá el motor de eventos. Este motor es quien se encarga de ejecutar realmente los eventos proporcionados por el motor anterior. Pueden existir eventos de diferentes tipos: que se ejecuten al momento, que ocurran una vez el jugador haya cruzado la mitad de una pantalla... estos eventos también pueden descartarse una vez hayan sucedido o que se mantengan en el tiempo, por ejemplo un evento que mate a un personaje, cambiando sus iconos en la pantalla, más adelante deberá conservarse la información de dicho personaje. Una vez ocurra cierto evento, por tanto, se deberá avisar al motor anterior para que vuelva a comprobar lo siguiente que deberá ocurrir.

Este sistema deberá de afinarse en base a las necesidades del videojuego, que se especificarán tanto en la historia como en las implementaciones.

### Guardado del juego.

El sistema de guardado de juego será simple. Por un lado se almacenará el mapa, su sección y posición del jugador en el que este se encontraba. Por otro lado se tendrá que almacenar también tanto las variables y sus valores por parte del jugador, como la cadena de eventos que el motor de eventos tuviera almacenados en ese momento. Del mismo modo se tendrá almacenados los ajustes del usuario en lo referente a la música, idioma... ect.
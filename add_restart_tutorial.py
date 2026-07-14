import xml.etree.ElementTree as ET
import os

langs = ['resx', 'es.resx', 'de.resx', 'gl.resx', 'gsw.resx']

translations = {
    'resx': 'Run Tutorial Again',
    'es.resx': 'Ejecutar tutorial de nuevo',
    'de.resx': 'Tutorial erneut starten',
    'gl.resx': 'Executar tutorial de novo',
    'gsw.resx': 'Tutorial nomol starta'
}

for lang in langs:
    filepath = f"src/Kairos.Shared/Resources/Strings.{lang}"
    tree = ET.parse(filepath)
    root = tree.getroot()

    # check if RestartTutorial already exists
    exists = False
    for data in root.findall('data'):
        if data.get('name') == 'RestartTutorial':
            exists = True
            break

    if not exists:
        new_data = ET.Element('data')
        new_data.set('name', 'RestartTutorial')
        new_data.set('xml:space', 'preserve')
        value = ET.SubElement(new_data, 'value')
        value.text = translations[lang]
        root.append(new_data)

        # Need to fix up indents
        ET.indent(tree, space="  ", level=0)
        tree.write(filepath, encoding='utf-8', xml_declaration=True)
        print(f"Added RestartTutorial to {lang}")

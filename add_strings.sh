#!/bin/bash
# A simple script to insert Export and FileName localized strings into the resx files

for file in src/Kairos.Application/Resources/Strings*.resx; do
    sed -i '/<\/root>/i \  <data name="Export" xml:space="preserve">\n    <value>Export</value>\n  </data>\n  <data name="FileName" xml:space="preserve">\n    <value>File name:</value>\n  </data>' $file
done

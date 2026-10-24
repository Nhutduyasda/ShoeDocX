"""Resolve built host paths from the project, never a historical framework constant."""
import xml.etree.ElementTree as ET

def host_dll(checkout, name):
    project = checkout / 'tests' / name / (name + '.csproj')
    target = ET.parse(project).getroot().findtext('./PropertyGroup/TargetFramework')
    assert target in ('net8.0', 'net10.0'), 'Unreviewed host framework'
    return project.parent / 'bin' / 'Debug' / target / (name + '.dll')

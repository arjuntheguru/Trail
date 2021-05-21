import React from 'react';
import { Modal } from 'react-bootstrap';
import TaskForm from './TaskForm';

const EditCompany = ({ show, onClose, data }) => {

    return (
        <Modal show={show} onHide={onClose} centered size="lg">           
            <Modal.Body className="bgGradient">{<TaskForm isEdit={true} data={data} onClose={onClose}/>}</Modal.Body>
        </Modal>
    )

}

export default EditCompany;